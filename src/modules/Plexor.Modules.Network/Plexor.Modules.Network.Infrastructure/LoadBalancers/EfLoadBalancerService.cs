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
/// </remarks>
/// <param name="db">Scoped <see cref="NetworkDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's stamps.</param>
public sealed class EfLoadBalancerService(
    NetworkDbContext db,
    TimeProvider clock) : ILoadBalancerService
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
