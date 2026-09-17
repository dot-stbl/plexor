// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoadBalancerSummary — projection for the list endpoint. Compact
// (no timestamps).
// ============================================================================

namespace Plexor.Modules.Network.Api.Models.Responses;

/// <summary>
///     Compact wire projection of
///     <c>Plexor.Modules.Network.Domain.Entities.LoadBalancer</c> for
///     list responses.
/// </summary>
public sealed class LoadBalancerSummary
{
    /// <summary>Load balancer id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Load balancer name (1-128 chars).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Layer (L4 / L7).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Scheduling algorithm.</summary>
    public string Algorithm { get; init; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public string Status { get; init; } = string.Empty;
}
