// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateLoadBalancerRequest — wire shape for POST
// /api/v1/network/load-balancers.
// ============================================================================

namespace Plexor.Modules.Network.Api.Models.Requests;

/// <summary>
///     Wire shape for the create-load-balancer request body. ClusterId
///     + Name + Type + Algorithm are required; Status defaults to
///     Pending.
/// </summary>
public sealed class CreateLoadBalancerRequest
{
    /// <summary>Target cluster id. The API layer validates the
    /// cluster exists before INSERT.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>Load balancer name (1-128 chars). Unique per cluster
    /// (enforced by the (cluster_id, name) UNIQUE index).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Layer — see <c>LoadBalancerType</c>. Wire shape
    /// accepts the enum name as a string (L4 / L7) for clarity.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Scheduling algorithm — see
    /// <c>LoadBalancerAlgorithm</c>. Wire shape accepts the enum
    /// name (RoundRobin / LeastConnections / etc.).</summary>
    public string Algorithm { get; init; } = string.Empty;
}
