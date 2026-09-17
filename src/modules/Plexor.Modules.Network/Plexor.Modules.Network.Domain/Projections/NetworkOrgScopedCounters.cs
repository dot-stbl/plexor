// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkOrgScopedCounters — projection returned by
// INetworkQuotaReader. Two counters per (OrgId): floating IP row
// count + load balancer row count. The quota enforcer combines
// these with the new resource's delta to decide Allowed /
// AllowedWithWarning / Denied for both network.floating_ips.count
// AND network.load_balancers.count.
//
// Record (not class) because the value is immutable + value-equal:
// the enforcer pattern-matches on the result and would otherwise
// allocate a defensive copy on every read.
// ============================================================================

namespace Plexor.Modules.Network.Domain.Projections;

/// <summary>
///     Per-org network counters — the load-bearing projection for
///     the network.floating_ips.count and
///     network.load_balancers.count quota keys.
/// </summary>
/// <param name="FloatingIpCount">Cumulative floating IP rows in the org.</param>
/// <param name="LoadBalancerCount">Cumulative load balancer rows in the org.</param>
public sealed record NetworkOrgScopedCounters(
    int FloatingIpCount,
    int LoadBalancerCount);
