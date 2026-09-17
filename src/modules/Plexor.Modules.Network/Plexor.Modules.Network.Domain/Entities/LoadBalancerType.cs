// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoadBalancerType — L4 vs L7. L4 load balancers terminate the TCP
// connection and forward at the transport layer; L7 load balancers
// parse + route at the HTTP/application layer. Plexor's v0.1 only
// provisions the row + flags the type; the runtime behaviour lands
// when the NodeAgent ships the matching proxy backend.
// ============================================================================

namespace Plexor.Modules.Network.Domain.Entities;

/// <summary>
///     Load-balancer layer. L4 = transport (TCP/UDP); L7 =
///     application (HTTP/HTTPS + path-based routing).
/// </summary>
public enum LoadBalancerType
{
    /// <summary>Layer 4 — transport-layer (TCP/UDP) load balancing.</summary>
    L4 = 0,

    /// <summary>Layer 7 — application-layer (HTTP/HTTPS) load balancing.</summary>
    L7 = 1,
}
