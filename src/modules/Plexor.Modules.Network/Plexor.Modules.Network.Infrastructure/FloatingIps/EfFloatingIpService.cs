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
/// </remarks>
/// <param name="db">Scoped <see cref="NetworkDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's stamps.</param>
public sealed class EfFloatingIpService(
    NetworkDbContext db,
    TimeProvider clock) : IFloatingIpService
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
