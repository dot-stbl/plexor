// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfLoadBalancerService — EF-backed ILoadBalancerService. Read +
// write surface for the load_balancers table. AsNoTracking on reads;
// scoped lifetime.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Network.Application.LoadBalancers;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Network.Infrastructure.LoadBalancers;

/// <summary>
///     EF-backed <see cref="ILoadBalancerService" />. AsNoTracking
///     reads on every list / get path; tracked entities only on the
///     create write path. Tenant scope is always <c>org_id = ?</c>.
/// </summary>
/// <remarks>
///     <para><b>Why public.</b> Same shape as
///     <c>Plexor.Modules.Quotas.Infrastructure.Quotas.EfQuotaScopeResolver</c>
///     — public so the unit-test project can construct it directly
///     with the in-memory <see cref="NetworkDbContext" />.</para>
///     <para><b>Quota enforcement (4.5.d follow-up).</b>
///     <see cref="CreateAsync" /> opens a transaction, reserves
///     <c>network.load_balancers.count</c> (amount = 1) at the org
///     scope, then INSERTs the load balancer row. The reservation +
///     INSERT commit (or roll back) together — a <c>Denied</c> throws
///     <see cref="QuotaExceededException" />, the open transaction
///     rolls back on dispose, and no row + no quota counter advances.</para>
/// </remarks>
/// <param name="db">Scoped <see cref="NetworkDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's stamps.</param>
/// <param name="quotaEnforcer">
///     Reserves <c>network.load_balancers.count</c> capacity inside
///     the same transaction as the load balancer INSERT — see 4.5.c
///     and <c>openspec/changes/phase-4-5-quotas/design.md</c> §"Enforcement:
///     pure-sync with pg_advisory_xact_lock".
/// </param>
/// <param name="currentUser">
///     Per-request caller identity (4.5.h). The service forwards
///     <see cref="ICurrentUser.UserId" /> into the
///     <see cref="QuotaScope" /> so the audit emitter can attach the
///     actor to every <c>UsageExceeded</c> / <c>LimitApproaching</c>
///     event.
/// </param>
public sealed class EfLoadBalancerService(
    NetworkDbContext db,
    TimeProvider clock,
    IQuotaEnforcer quotaEnforcer,
    ICurrentUser currentUser) : ILoadBalancerService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LoadBalancer>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await db.LoadBalancers
            .AsNoTracking()
            .Where(lb => lb.OrgId == orgId)
            .OrderByDescending(static lb => lb.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<LoadBalancer?> GetAsync(
        Guid loadBalancerId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return db.LoadBalancers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                lb => lb.Id == loadBalancerId && lb.OrgId == orgId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LoadBalancer> CreateAsync(
        NewLoadBalancerInput input,
        CancellationToken cancellationToken = default)
    {
        // Single transaction: the enforcer's UPDATE on quotas.quota_usage
        // + the load balancer INSERT commit (or roll back) together.
        // Both contexts share the same NpgsqlDataSource via the
        // PlexorDataSourceExtensions composition root; the InMemory
        // provider used by handler unit tests is skipped (no
        // transaction support).
        await using var transaction =
            await db.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Reserve network.load_balancers.count at the org scope
        // (amount = 1). ActorUserId flows into the QuotaScope so the
        // audit emitter can attach the caller to any UsageExceeded /
        // LimitApproaching event. A Denied throws QuotaExceededException
        // and the open transaction rolls back on dispose before the
        // exception propagates — no INSERT, no quota counter advance.
        var quotaCheck = await quotaEnforcer.CheckAndReserveAsync(
            QuotaScope.Org(input.OrgId, currentUser.UserId),
            QuotaDefinitionKey.LoadBalancersCount,
            amount: 1,
            cancellationToken);
        if (quotaCheck is QuotaCheckResult.Denied denied)
        {
            throw new QuotaExceededException(
                denied.Limit,
                denied.Used,
                denied.Requested);
        }

        var now = clock.GetUtcNow();
        var entity = new LoadBalancer
        {
            Id = Guid.NewGuid(),
            OrgId = input.OrgId,
            ClusterId = input.ClusterId,
            Name = input.Name,
            Type = input.Type,
            Algorithm = input.Algorithm,
            Status = NetworkResourceStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.LoadBalancers.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return entity;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid loadBalancerId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.LoadBalancers
            .FirstOrDefaultAsync(
                lb => lb.Id == loadBalancerId && lb.OrgId == orgId,
                cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.LoadBalancers.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
