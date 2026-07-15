// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCommandHandlers — NodeJoin / NodeHeartbeat / ListNodes. The
// NodeAgent-facing surface. NodeJoin is the only endpoint that runs
// without a node-bearer token (it authenticates via the one-time join
// token); the others require a valid node-bearer token.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     Redeem a join token. Token lookup via Repository (read);
///     cluster status check via DbContext (conditional update shape);
///     node insert + token revocation via DbContext (writes).
/// </summary>
/// <param name="db">ClusterDbContext for writes + cluster lookup.</param>
/// <param name="tokenRepo">Read surface for token-by-hash lookup.</param>
public sealed class NodeJoinCommandHandler(
    ClusterDbContext db,
    Repository<JoinToken> tokenRepo) : ICommandHandler<NodeJoinCommand, NodeJoinResult>
{
    /// <inheritdoc />
    public async Task<NodeJoinResult> HandleAsync(
        NodeJoinCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(command.JoinToken))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                "Join token is required.");
        }

        var tokenHash = await TokenHasher.HashAsync(command.JoinToken, cancellationToken);

        // Repository read: any row matching the hash (active or otherwise;
        // status check happens after fetch so we can distinguish
        // "revoked" from "never existed" with a single column lookup).
        if (await tokenRepo.FirstOrDefaultAsync(
                new JoinTokenByHashSpec(tokenHash),
                cancellationToken) is not { } token
            || token.Status != TokenStatus.Active)
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                "Join token is invalid, revoked, or expired.");
        }

        if (token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                "Join token is expired.");
        }

        if (token.IntendedRole != command.Role)
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                $"Token is for role '{token.IntendedRole}' but node requested '{command.Role}'.");
        }

        if (await db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.Id == token.ClusterId)
            .Select(cluster => new { cluster.Id, cluster.OrgId, cluster.Endpoint, cluster.Status })
            .FirstOrDefaultAsync(cancellationToken) is not { } cluster || cluster.Status == ClusterStatus.Offline)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{token.ClusterId}' not found or offline.");
        }

        if (await db.Nodes.AsNoTracking().AnyAsync(
                node => node.ClusterId == token.ClusterId && node.Hostname == command.Hostname,
                cancellationToken))
        {
            throw new ClustersException(
                ClustersExceptions.NodeHostnameTaken,
                $"Hostname '{command.Hostname}' is already taken in this cluster.");
        }

        var now = DateTimeOffset.UtcNow;
        var nodeId = Guid.NewGuid();
        var node = new Node
        {
            Id = nodeId,
            ClusterId = cluster.Id,
            OrgId = cluster.OrgId,
            Hostname = command.Hostname,
            Role = command.Role,
            Status = NodeStatus.Ready,
            Spec = command.Hardware,
            LastHeartbeatAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Mark the join token as consumed (one-time use). The node id
        // is recorded for the audit trail. Tracked entity write (not
        // ExecuteUpdate) so the handler works on both npgsql + InMemory.
        db.Entry(token).Property(static jt => jt.Status).CurrentValue = TokenStatus.Revoked;
        db.Entry(token).Property(static jt => jt.RedeemedByNodeId).CurrentValue = nodeId;

        await db.Nodes.AddAsync(node, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Node-bearer token (v0.1: opaque random; Phase 5+ signs a JWT
        // via the Sigil module). Returned once; NodeAgent persists it.
        var nodeToken = TokenHasher.NewSecret();

        // WireGuard config blob — v0.1 returns an empty placeholder.
        // Phase 5+ generates a real wg-quick.conf once the mesh automation
        // is wired up (plan-clusters.md "Out of scope").
        var wireguardConfig = string.Empty;

        return new NodeJoinResult(
            nodeId,
            cluster.Id,
            nodeToken,
            cluster.Endpoint,
            wireguardConfig);
    }
}

/// <summary>
///     Stamp a node's heartbeat. Sets <see cref="Node.LastHeartbeatAt" />
///     to now + flips <see cref="Node.Status" /> to
///     <see cref="NodeStatus.Ready" />. Returns the cluster status so
///     NodeAgent can react (e.g. drain + exit on
///     <see cref="ClusterStatus.Offline" />). List nodes in one
///     cluster — see ClusterReadHandlers.cs (moved to
///     <see cref="ListNodesQueryHandler" /> for Repository pattern).
/// </summary>
/// <param name="db"></param>
public sealed class NodeHeartbeatCommandHandler(
    ClusterDbContext db) : ICommandHandler<NodeHeartbeatCommand, NodeHeartbeatResult>
{
    /// <inheritdoc />
    public async Task<NodeHeartbeatResult> HandleAsync(
        NodeHeartbeatCommand command,
        CancellationToken cancellationToken = default)
    {

        if (await db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.Id == command.ClusterId)
            .Select(cluster => (ClusterStatus?)cluster.Status)
            .FirstOrDefaultAsync(cancellationToken) is not { } clusterStatus)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");
        }

        if (await db.Nodes.FirstOrDefaultAsync(
                node => node.Id == command.NodeId && node.ClusterId == command.ClusterId,
                cancellationToken) is not { } node)
        {
            throw new ClustersException(
                ClustersExceptions.NodeNotFound,
                $"Node '{command.NodeId}' in cluster '{command.ClusterId}' not found.");
        }

        var now = DateTimeOffset.UtcNow;
        db.Entry(node).Property(static n => n.LastHeartbeatAt).CurrentValue = now;
        // Don't flip a draining node back to Ready mid-drain — operators
        // want the drain to complete cleanly. Likewise an Offline cluster
        // means the host is unreachable; keep the node in its terminal state.
        if (node.Status != NodeStatus.Draining && clusterStatus != ClusterStatus.Offline)
        {
            db.Entry(node).Property(static n => n.Status).CurrentValue = NodeStatus.Ready;
        }
        db.Entry(node).Property(static n => n.Spec).CurrentValue = command.Hardware;
        db.Entry(node).Property(static n => n.UpdatedAt).CurrentValue = now;
        await db.SaveChangesAsync(cancellationToken);

        return new NodeHeartbeatResult(command.NodeId, clusterStatus, DateTimeOffset.UtcNow);
    }
}


