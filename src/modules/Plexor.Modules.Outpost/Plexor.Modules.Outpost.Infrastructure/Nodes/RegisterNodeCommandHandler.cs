// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostCommandHandlers — Register + Heartbeat + List + Get.
//
// Outpost owns the node tracking surface; the join flow was extracted
// from Plexor.Modules.Clusters.Infrastructure.Clusters
// .NodeCommandHandlers.cs as part of the Outpost extraction. The
// workload-reconciliation logic from the previous heartbeat handler
// stays in Clusters (it's a Clusters concern — worktables reference
// Clusters state, not node state).
//
// Two DbContexts cooperate here:
//   * ClusterDbContext — reads clusters + join tokens (forge schema)
//   * OutpostDbContext — writes node_records (outpost schema)
// Both share a single NpgsqlDataSource via AddPlexorDataSource so
// cross-DbContext transactions work; v0.1 issues two SaveChanges calls
// (one per schema) and the join semantics are "token revoked BEFORE
// node inserted" — failure between the two leaves the token revoked
// and the node absent, which is a recoverable state on retry.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Modules.Outpost.Application.NodeCommands;
using Plexor.Modules.Outpost.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Outpost.Infrastructure.Nodes;

/// <summary>
///     Redeem a join token. Token lookup via Repository (read);
///     cluster status check + node insert + token revocation via
///     DbContext (writes).
/// </summary>
/// <param name="outpostDb">OutpostDbContext for node insert.</param>
/// <param name="clusterDb">ClusterDbContext for cluster lookup + token revocation.</param>
/// <param name="tokenRepo">Read surface for token-by-hash lookup.</param>
public sealed class RegisterNodeCommandHandler(
    OutpostDbContext outpostDb,
    ClusterDbContext clusterDb,
    Repository<JoinToken> tokenRepo) : ICommandHandler<RegisterNodeCommand, RegisterNodeResult>
{
    /// <inheritdoc />
    public async Task<RegisterNodeResult> HandleAsync(
        RegisterNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.JoinToken))
        {
            throw new OutpostException(
                OutpostExceptions.InvalidJoinToken,
                "Join token is required.");
        }

        var tokenHash = await TokenHasher.HashAsync(command.JoinToken, cancellationToken);

        if (await tokenRepo.FirstOrDefaultAsync(
                new JoinTokenByHashSpec(tokenHash),
                cancellationToken) is not { } token
            || token.Status != TokenStatus.Active)
        {
            throw new OutpostException(
                OutpostExceptions.InvalidJoinToken,
                "Join token is invalid, revoked, or expired.");
        }

        if (token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new OutpostException(
                OutpostExceptions.InvalidJoinToken,
                "Join token is expired.");
        }

        if (token.IntendedRole != command.Role)
        {
            throw new OutpostException(
                OutpostExceptions.NodeRoleMismatch,
                $"Token is for role '{token.IntendedRole}' but node requested '{command.Role}'.");
        }

        if (await clusterDb.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.Id == token.ClusterId)
            .Select(cluster => new { cluster.Id, cluster.OrgId, cluster.Endpoint, cluster.Status })
            .FirstOrDefaultAsync(cancellationToken) is not { } cluster || cluster.Status == ClusterStatus.Offline)
        {
            throw new OutpostException(
                OutpostExceptions.ClusterNotFound,
                $"Cluster '{token.ClusterId}' not found or offline.");
        }

        if (await outpostDb.NodeRecords.AsNoTracking().AnyAsync(
                node => node.ClusterId == token.ClusterId && node.Hostname == command.Hostname,
                cancellationToken))
        {
            throw new OutpostException(
                OutpostExceptions.NodeHostnameTaken,
                $"Hostname '{command.Hostname}' is already taken in this cluster.");
        }

        var now = DateTimeOffset.UtcNow;
        var nodeId = IdGenerator.NewNodeId();
        var node = new NodeRecord
        {
            Id = nodeId,
            ClusterId = cluster.Id,
            OrgId = cluster.OrgId,
            Hostname = command.Hostname,
            IpAddress = command.IpAddress,
            Role = command.Role,
            Status = NodeStatus.Ready,
            Spec = command.Spec,
            IsoVersion = command.IsoVersion,
            WireguardPublicKey = command.WireguardPublicKey,
            LastHeartbeatAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Mark the join token as consumed (one-time use). Tracked
        // entity write (not ExecuteUpdate) so the handler works on
        // both npgsql + InMemory.
        clusterDb.Entry(token).Property(static jt => jt.Status).CurrentValue = TokenStatus.Revoked;
        clusterDb.Entry(token).Property(static jt => jt.RedeemedByNodeId).CurrentValue = nodeId;
        await clusterDb.SaveChangesAsync(cancellationToken);

        await outpostDb.NodeRecords.AddAsync(node, cancellationToken);
        await outpostDb.SaveChangesAsync(cancellationToken);

        // v0.1 — opaque random node-bearer token; Phase 5+ signs a
        // JWT via the Sigil module. Returned once; NodeAgent
        // persists it.
        var nodeToken = TokenHasher.NewSecret();

        return new RegisterNodeResult(node, nodeToken, cluster.Endpoint);
    }
}