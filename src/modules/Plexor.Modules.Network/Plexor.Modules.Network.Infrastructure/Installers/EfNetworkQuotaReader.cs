// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfNetworkQuotaReader — Phase 4.5.d EF Core implementation of
// INetworkQuotaReader. Read-only lookups against network.floating_ips
// + network.load_balancers. AsNoTracking + two single-row COUNT(*)
// aggregates scoped to (org_id) — the quota enforcer calls this on
// every CreateFloatingIp / CreateLoadBalancer path.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Network.Application.Network;
using Plexor.Modules.Network.Domain.Projections;
using Plexor.Modules.Network.Infrastructure.Persistence;

namespace Plexor.Modules.Network.Infrastructure.Installers;

/// <summary>
///     EF Core implementation of <see cref="INetworkQuotaReader" />.
///     Uses <see cref="NetworkDbContext" /> with <c>AsNoTracking()</c>
///     reads — no change tracker involvement.
/// </summary>
/// <param name="db">Scoped <see cref="NetworkDbContext" />.</param>
internal sealed class EfNetworkQuotaReader(NetworkDbContext db) : INetworkQuotaReader
{
    /// <inheritdoc />
    public async Task<NetworkOrgScopedCounters> CountAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        // Two single-row COUNTs on the (org_id) indexes. Doing them
        // in one round-trip via Task.WhenAll (in the caller) or
        // sequentially here — sequential keeps the LINQ tree
        // trivial for the InMemory provider used in unit tests.
        var floatingIpCount = await db.FloatingIps
            .AsNoTracking()
            .CountAsync(ip => ip.OrgId == orgId, cancellationToken);

        var loadBalancerCount = await db.LoadBalancers
            .AsNoTracking()
            .CountAsync(lb => lb.OrgId == orgId, cancellationToken);

        return new NetworkOrgScopedCounters(
            FloatingIpCount: floatingIpCount,
            LoadBalancerCount: loadBalancerCount);
    }
}
