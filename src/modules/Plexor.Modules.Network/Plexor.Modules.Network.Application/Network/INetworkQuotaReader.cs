// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// INetworkQuotaReader — read-only seam over Plexor.Modules.Network for
// the Quotas module's EfQuotaEnforcer. The enforcer needs to know
// "current floating IP count" + "current load balancer count" per org
// on every CreateFloatingIp / CreateLoadBalancer path; this seam gives
// it that without the enforcer taking a project reference to
// Plexor.Modules.Network.Infrastructure (which would break Law 3 —
// cross-module coupling).
//
// Mirrors IStorageQuotaReader (Storage → Quotas): the owning module
// (Network) defines the seam in its Application layer; the consumer
// (Quotas.Infrastructure) implements it from Network.Infrastructure.
//
// Lifetime: Scoped (mirrors NetworkDbContext — reads share the
// per-request scope so the enforcer's advisory lock + INSERT +
// network counter SELECT all ride one physical connection).
// ============================================================================

using Plexor.Modules.Network.Domain.Projections;

namespace Plexor.Modules.Network.Application.Network;

/// <summary>
///     Read-only access to <see cref="NetworkOrgScopedCounters" />
///     for callers outside the Network module. The Quotas module's
///     <c>EfQuotaEnforcer.CheckAndReserveAsync</c> resolves this
///     interface per-call to enforce the
///     <c>network.floating_ips.count</c> and
///     <c>network.load_balancers.count</c> quota keys.
/// </summary>
public interface INetworkQuotaReader
{
    /// <summary>
    ///     Count the floating-IP rows + load-balancer rows for a given
    ///     organization. Returns a default-initialised
    ///     <see cref="NetworkOrgScopedCounters" /> when no rows exist
    ///     (a brand-new org with zero of either).
    /// </summary>
    /// <param name="orgId">Tenant scope to count.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<NetworkOrgScopedCounters> CountAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);
}
