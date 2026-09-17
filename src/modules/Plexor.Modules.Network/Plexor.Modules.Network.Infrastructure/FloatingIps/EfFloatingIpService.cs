// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfFloatingIpService — EF-backed IFloatingIpService. Read + write
// surface for the floating_ips table. AsNoTracking on reads; scoped
// lifetime (matches the per-request DbContext).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Network.Application.FloatingIps;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Network.Infrastructure.FloatingIps;

/// <summary>
///     EF-backed <see cref="IFloatingIpService" />. AsNoTracking reads
///     on every list / get path; tracked entities only on the
///     create write path. Tenant scope is always <c>org_id = ?</c>.
/// </summary>
/// <remarks>
///     <para><b>Why public.</b> Same shape as
///     <c>Plexor.Modules.Quotas.Infrastructure.Quotas.EfQuotaScopeResolver</c>
///     — public so the unit-test project can construct it directly
///     with the in-memory <see cref="NetworkDbContext" />.</para>
///     <para><b>Quota enforcement (4.5.d follow-up).</b>
///     <see cref="CreateAsync" /> opens a transaction, reserves
///     <c>network.floating_ips.count</c> (amount = 1) at the org
///     scope, then INSERTs the floating IP row. The reservation +
///     INSERT commit (or roll back) together — a <c>Denied</c> throws
///     <see cref="QuotaExceededException" />, the open transaction
///     rolls back on dispose, and no row + no quota counter advances.</para>
/// </remarks>
/// <param name="db">Scoped <see cref="NetworkDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's stamps.</param>
/// <param name="quotaEnforcer">
///     Reserves <c>network.floating_ips.count</c> capacity inside the
///     same transaction as the floating IP INSERT — see 4.5.c and
///     <c>openspec/changes/phase-4-5-quotas/design.md</c> §"Enforcement:
///     pure-sync with pg_advisory_xact_lock".
/// </param>
/// <param name="currentUser">
///     Per-request caller identity (4.5.h). The service forwards
///     <see cref="ICurrentUser.UserId" /> into the
///     <see cref="QuotaScope" /> so the audit emitter can attach the
///     actor to every <c>UsageExceeded</c> / <c>LimitApproaching</c>
///     event.
/// </param>
public sealed class EfFloatingIpService(
    NetworkDbContext db,
    TimeProvider clock,
    IQuotaEnforcer quotaEnforcer,
    ICurrentUser currentUser) : IFloatingIpService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FloatingIp>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await db.FloatingIps
            .AsNoTracking()
            .Where(ip => ip.OrgId == orgId)
            .OrderByDescending(static ip => ip.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<FloatingIp?> GetAsync(
        Guid floatingIpId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return db.FloatingIps
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ip => ip.Id == floatingIpId && ip.OrgId == orgId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FloatingIp> CreateAsync(
        NewFloatingIpInput input,
        CancellationToken cancellationToken = default)
    {
        // Single transaction: the enforcer's UPDATE on quotas.quota_usage
        // + the floating IP INSERT commit (or roll back) together.
        // Both contexts share the same NpgsqlDataSource via the
        // PlexorDataSourceExtensions composition root; the InMemory
        // provider used by handler unit tests is skipped (no
        // transaction support).
        await using var transaction =
            await db.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Reserve network.floating_ips.count at the org scope
        // (amount = 1). ActorUserId flows into the QuotaScope so the
        // audit emitter can attach the caller to any UsageExceeded /
        // LimitApproaching event. A Denied throws QuotaExceededException
        // and the open transaction rolls back on dispose before the
        // exception propagates — no INSERT, no quota counter advance.
        var quotaCheck = await quotaEnforcer.CheckAndReserveAsync(
            QuotaScope.Org(input.OrgId, currentUser.UserId),
            QuotaDefinitionKey.FloatingIpsCount,
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
        var entity = new FloatingIp
        {
            Id = Guid.NewGuid(),
            OrgId = input.OrgId,
            ClusterId = input.ClusterId,
            Address = input.Address,
            Status = NetworkResourceStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.FloatingIps.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return entity;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid floatingIpId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.FloatingIps
            .FirstOrDefaultAsync(
                ip => ip.Id == floatingIpId && ip.OrgId == orgId,
                cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.FloatingIps.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
