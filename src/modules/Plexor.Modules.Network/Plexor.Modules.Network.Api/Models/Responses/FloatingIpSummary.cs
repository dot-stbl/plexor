// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FloatingIpSummary — projection for the list endpoint. Same as
// FloatingIpDetail minus the timestamps + OrgId + ClusterId that the
// admin table view doesn't need.
// ============================================================================

namespace Plexor.Modules.Network.Api.Models.Responses;

/// <summary>
///     Compact wire projection of
///     <c>Plexor.Modules.Network.Domain.Entities.FloatingIp</c> for
///     list responses.
/// </summary>
public sealed class FloatingIpSummary
{
    /// <summary>Floating IP id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>IPv4 or IPv6 address (textual).</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>Lifecycle status — see <c>NetworkResourceStatus</c>.</summary>
    public string Status { get; init; } = string.Empty;
}
