// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterCommandHandlers — CreateCluster / UpdateCluster / DeleteCluster /
// GetCluster / ListClusters / RotateJoinToken. Co-located in one file
// because every handler depends on the same ClusterDbContext and the
// bodies are < 80 lines each. Pattern mirrors the Sigil module's
// UserCommandHandlers.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Quotas;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     Create a cluster. Issues a fresh join token (7-day TTL) so
///     the first NodeAgent can redeem it immediately. Returns the
///     <see cref="JoinTokenResult" /> for the operator's join-landing
///     page.
/// </summary>
/// <param name="db">EF Core context for the resource-create transaction.</param>
/// <param name="quotaEnforcer">
///     Reserves <c>compute.clusters.count</c> capacity inside the same
///     transaction as the cluster INSERT — see 4.5.c and
///     <c>openspec/changes/phase-4-5-quotas/design.md</c> §"Enforcement:
///     pure-sync with pg_advisory_xact_lock".
/// </param>
/// <param name="currentUser">
///     Per-request caller identity (4.5.h). The handler forwards
///     <see cref="ICurrentUser.UserId" /> into the
///     <see cref="QuotaScope" /> so the audit emitter can attach the
///     actor to every <c>UsageExceeded</c> / <c>LimitApproaching</c>
///     event.
/// </param>
/// <remarks>
///     <para><b>Why a transaction wraps the whole handler.</b> The
///     enforcer's <c>UPDATE quotas.quota_usage</c> + the cluster +
///     join-token INSERTs share one Postgres connection (via the
///     shared <c>NpgsqlDataSource</c>; see
///     <see cref="Plexor.Shared.Persistence.PlexorDataSourceExtensions" />)
///     and commit / roll back together. A duplicate-name collision
///     inside the uniqueness check, or any other pre-commit failure,
///     rolls back the reserved quota automatically — the caller never
///     sees a quota counter that disagrees with the live resource
///     set.</para>
///     <para><b>Why the enforcer runs before the uniqueness check.</b>
///     A request that's over-quota fails fast on the enforcer's
///     <c>Denied</c> without touching the uniqueness SELECT. A request
///     that's within quota but collides on name rolls back the
///     reservation cleanly because the open transaction disposes
///     before the exception propagates.</para>
///     <para><b>ICurrentUser dependency (4.5.h).</b> The interface lives
///     in <c>Plexor.Modules.Sigil.Application.Abstractions</c>; the
///     handler depends on the Application layer only, never on the
///     Sigil Infrastructure layer (the <c>HttpContextCurrentUser</c>
///     implementation). Forward plan: move ICurrentUser into
///     <c>Plexor.Shared.Kernel</c> so this cross-module reference
///     becomes a same-module reference.</para>
/// </remarks>
public sealed class CreateClusterCommandHandler(
    ClusterDbContext db,
    IQuotaEnforcer quotaEnforcer,
    ICurrentUser currentUser) : ICommandHandler<CreateClusterCommand, JoinTokenResult>
{
    /// <inheritdoc />
    public async Task<JoinTokenResult> HandleAsync(
        CreateClusterCommand command,
        CancellationToken cancellationToken = default)
    {
        // Cheap in-memory validation — no DB hit, no quota cost yet.
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNameTaken,
                "Cluster name is required.");
        }

        if (!ClusterRuntimeIds.IsValid(command.RuntimeId))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidRuntimeId,
                $"Cluster runtime id '{command.RuntimeId}' is not supported. " +
                "Expected one of: docker-compose, podman-quadlet, k3s.");
        }

        // Single transaction: the enforcer's UPDATE on quotas.quota_usage
        // and the cluster + join-token INSERTs commit (or roll back)
        // together. Both contexts (ClusterDbContext + QuotasDbContext)
        // draw from the shared NpgsqlDataSource. The transaction is
        // skipped against non-relational providers (e.g. the InMemory
        // provider used by handler unit tests) — production runs
        // always go through the transactional path.
        await using var transaction =
            await db.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Pre-check + reserve BEFORE the uniqueness SELECT. The enforcer
        // acquires pg_advisory_xact_lock at scope granularity + upserts
        // quota_usage + bumps current_value. If the scope is over
        // limit, the open transaction rolls back via Dispose (no
        // INSERT was made yet). The ActorUserId flows into the
        // QuotaScope so the audit emitter can attach the caller to
        // any UsageExceeded / LimitApproaching event.
        var quotaCheck = await quotaEnforcer.CheckAndReserveAsync(
            QuotaScope.Org(command.OrgId, currentUser.UserId),
            QuotaDefinitionKey.ClustersCount,
            amount: 1,
            cancellationToken);

        if (quotaCheck is QuotaCheckResult.Denied denied)
        {
            throw new QuotaExceededException(
                denied.Limit,
                denied.Used,
                denied.Requested);
        }

        // Uniqueness check now runs inside the transaction so a
        // duplicate-name collision rolls back the reserved quota.
        if (await db.Clusters.AsNoTracking().AnyAsync(
                cluster => cluster.OrgId == command.OrgId && cluster.Name == command.Name,
                cancellationToken))
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNameTaken,
                $"Cluster name '{command.Name}' is already taken in this org.");
        }

        var now = DateTimeOffset.UtcNow;
        var clusterId = IdGenerator.NewClusterId();
        var cluster = new Cluster
        {
            Id = clusterId,
            OrgId = command.OrgId,
            Name = command.Name,
            Region = command.Region,
            Status = ClusterStatus.Pending,
            RuntimeId = command.RuntimeId,
            JoinTokenExpiresAt = now.AddDays(7),
            CreatedAt = now,
            UpdatedAt = now,
        };

        var tokenSecret = TokenHasher.NewSecret();
        var tokenHash = await TokenHasher.HashAsync(tokenSecret, cancellationToken);
        var joinToken = new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = clusterId,
            OrgId = command.OrgId,
            Label = $"initial ({command.InitialNodeRole})",
            Status = TokenStatus.Active,
            TokenHash = tokenHash,
            IntendedRole = command.InitialNodeRole,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        };

        await db.Clusters.AddAsync(cluster, cancellationToken);
        await db.JoinTokens.AddAsync(joinToken, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new JoinTokenResult(
            clusterId,
            tokenSecret,
            joinToken.ExpiresAt,
            cluster.Endpoint);
    }
}

/// <summary>
///     Update a cluster's name + region. Cannot change
///     <see cref="Cluster.OrgId" /> (cross-tenant migration is Phase 2+).
/// </summary>
/// <param name="db"></param>
/// <param name="mapper"></param>
public sealed class UpdateClusterCommandHandler(
    ClusterDbContext db,
    IClusterMapper mapper) : ICommandHandler<UpdateClusterCommand, ClusterSummary>
{
    /// <inheritdoc />
    public async Task<ClusterSummary> HandleAsync(
        UpdateClusterCommand command,
        CancellationToken cancellationToken = default)
    {

        if (command.Name is { } newName)
        {
            // Load the target cluster to know its org (the rename
            // uniqueness check must stay within the same org).
            if (await db.Clusters
                .AsNoTracking()
                .Where(cluster => cluster.Id == command.ClusterId)
                .Select(cluster => new { cluster.OrgId })
                .FirstOrDefaultAsync(cancellationToken) is not { } target)
            {
                throw new ClustersException(
                    ClustersExceptions.ClusterNotFound,
                    $"Cluster '{command.ClusterId}' not found.");
            }

            var conflict = await db.Clusters.AsNoTracking().AnyAsync(
                cluster => cluster.OrgId == target.OrgId &&
                           cluster.Name == newName &&
                           cluster.Id != command.ClusterId,
                cancellationToken);
            if (conflict)
            {
                throw new ClustersException(
                    ClustersExceptions.ClusterNameTaken,
                    $"Cluster name '{newName}' is already taken in this org.");
            }
        }

        var updated = await db.Clusters
            .Where(cluster => cluster.Id == command.ClusterId)
            .ExecuteUpdateAsync(
                setters =>
                {
                    if (command.Name is not null)
                    {
                        setters.SetProperty(cluster => cluster.Name, command.Name);
                    }
                    if (command.Region is not null)
                    {
                        setters.SetProperty(cluster => cluster.Region, command.Region);
                    }
                    setters.SetProperty(cluster => cluster.UpdatedAt, DateTimeOffset.UtcNow);
                },
                cancellationToken);
        if (updated == 0)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");
        }

        var cluster = await db.Clusters.AsNoTracking()
            .FirstAsync(c => c.Id == command.ClusterId, cancellationToken);
        return mapper.ToSummary(cluster);
    }
}

/// <summary>
///     Get one cluster by id with its child nodes loaded. — see ClusterReadHandlers.cs.
///     List clusters in one org, paged — see ClusterReadHandlers.cs.
///     Soft-delete a cluster. Cascades <c>Node.Status = Gone</c> on
///     every child node. The cluster row stays for audit + FK
///     integrity; no hard delete in v0.1.
/// </summary>
/// <param name="db"></param>
public sealed class DeleteClusterCommandHandler(
    ClusterDbContext db) : ICommandHandler<DeleteClusterCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(
        DeleteClusterCommand command,
        CancellationToken cancellationToken = default)
    {

        if (await db.Clusters.FirstOrDefaultAsync(
                cluster => cluster.Id == command.ClusterId,
                cancellationToken) is not { } cluster)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");
        }

        var now = DateTimeOffset.UtcNow;
        db.Entry(cluster).Property(static c => c.Status).CurrentValue = ClusterStatus.Offline;
        db.Entry(cluster).Property(static c => c.UpdatedAt).CurrentValue = now;

        var nodes = await db.Nodes
            .Where(node => node.ClusterId == command.ClusterId)
            .ToArrayAsync(cancellationToken);
        foreach (var node in nodes)
        {
            db.Entry(node).Property(static n => n.Status).CurrentValue = NodeStatus.Gone;
            db.Entry(node).Property(static n => n.UpdatedAt).CurrentValue = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
///     Rotate a cluster's join token. Old active tokens are revoked;
///     the new one is returned with a 7-day TTL. Used by the join-
///     landing page in the console.
/// </summary>
/// <param name="db"></param>
public sealed class RotateJoinTokenCommandHandler(
    ClusterDbContext db) : ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>
{
    /// <inheritdoc />
    public async Task<JoinTokenResult> HandleAsync(
        RotateJoinTokenCommand command,
        CancellationToken cancellationToken = default)
    {

        if (await db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.Id == command.ClusterId)
            .Select(cluster => new { cluster.Id, cluster.OrgId, cluster.Endpoint })
            .FirstOrDefaultAsync(cancellationToken) is not { } cluster)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");
        }

        await db.JoinTokens
            .Where(token => token.ClusterId == command.ClusterId && token.Status == TokenStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.Status, TokenStatus.Revoked),
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var tokenSecret = TokenHasher.NewSecret();
        var tokenHash = await TokenHasher.HashAsync(tokenSecret, cancellationToken);
        var joinToken = new JoinToken
        {
            Id = IdGenerator.NewTokenId(),
            ClusterId = command.ClusterId,
            OrgId = cluster.OrgId,
            Label = $"rotated at {now:O}",
            Status = TokenStatus.Active,
            TokenHash = tokenHash,
            IntendedRole = NodeRole.Compute,
            IssuedAt = now,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        };

        await db.JoinTokens.AddAsync(joinToken, cancellationToken);
        await db.Clusters
            .Where(c => c.Id == command.ClusterId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(c => c.JoinTokenExpiresAt, joinToken.ExpiresAt),
                cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new JoinTokenResult(
            command.ClusterId,
            tokenSecret,
            joinToken.ExpiresAt,
            cluster.Endpoint);
    }
}

/// <summary>Unit type placeholder for commands that return no value.</summary>
public sealed class Unit
{
    /// <summary>Singleton instance.</summary>
    public static readonly Unit Value = new();

    private Unit() { }
}
