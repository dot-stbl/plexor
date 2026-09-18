// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FloatingIpDetail — full projection for the GET-by-id endpoint.
// Includes OrgId + ClusterId + the row's timestamps so the detail
// view can render its provenance.
// ============================================================================

namespace Plexor.Modules.Network.Api.Models.Responses;

/// <summary>
///     Full wire projection of
///     <c>Plexor.Modules.Network.Domain.Entities.FloatingIp</c> for the
///     detail endpoint.
/// </summary>
public sealed class FloatingIpDetail
{
    /// <summary>Floating IP id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Target cluster id.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>IPv4 or IPv6 address (textual).</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
