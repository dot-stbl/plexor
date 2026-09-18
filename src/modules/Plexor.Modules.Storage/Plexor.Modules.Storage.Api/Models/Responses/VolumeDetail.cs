// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VolumeDetail — full wire projection of Volume for GET-by-id. Same
// fields as VolumeSummary + OrgId + ClusterId + CreatedAt + UpdatedAt
// so the detail view can render its provenance.
// ============================================================================

namespace Plexor.Modules.Storage.Api.Models.Responses;

/// <summary>
///     Full wire projection of <c>Plexor.Modules.Storage.Domain.Entities.Volume</c>
///     for the detail endpoint. Adds OrgId + ClusterId + the row's
///     timestamps so the detail view can render its provenance.
/// </summary>
public sealed class VolumeDetail
{
    /// <summary>Volume id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Target cluster id.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>Volume name (1-128 chars).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Volume size in GiB.</summary>
    public int SizeGb { get; init; }

    /// <summary>Lifecycle status — see <c>VolumeStatus</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
