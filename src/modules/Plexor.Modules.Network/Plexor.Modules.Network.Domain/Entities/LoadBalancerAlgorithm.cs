// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoadBalancerAlgorithm — the scheduling algorithm the LB uses to
// pick a backend for each incoming connection. Plexor's v0.1 flags
// the choice; the actual scheduling lands in the NodeAgent's proxy
// backend (HAProxy / nginx / envoy) when the runtime work ships.
// ============================================================================

namespace Plexor.Modules.Network.Domain.Entities;

/// <summary>
///     Load-balancing algorithm — the scheduling policy the LB uses to
///     pick a backend per connection.
/// </summary>
public enum LoadBalancerAlgorithm
{
    /// <summary>Round-robin: cycle through backends in order.</summary>
    RoundRobin = 0,

    /// <summary>Least connections: pick the backend with fewest
    /// in-flight connections.</summary>
    LeastConnections = 1,

    /// <summary>Source-IP hash: pick a deterministic backend per
    /// source IP (sticky sessions without a cookie).</summary>
    SourceIpHash = 2,

    /// <summary>Weighted round-robin: backends carry weights; the
    /// scheduler hands out proportionally.</summary>
    WeightedRoundRobin = 3,
}
