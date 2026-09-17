// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VolumeSummary — projection returned by GET /api/v1/storage/volumes
// (list). Same shape as VolumeDetail minus the timestamps that the
// list endpoint doesn't show — keeps the wire payload compact for the
// admin table view.
// ============================================================================

namespace Plexor.Modules.Storage.Api.Models.Responses;

/// <summary>
///     Compact wire projection of <c>Plexor.Modules.Storage.Domain.Entities.Volume</c>
///     for list responses. Excludes timestamps + cluster id; the admin
///     table view doesn't need them.
/// </summary>
public sealed class VolumeSummary
{
    /// <summary>Volume id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Volume name (1-128 chars).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Volume size in GiB.</summary>
    public int SizeGb { get; init; }

    /// <summary>Lifecycle status — see <c>VolumeStatus</c>.</summary>
    public string Status { get; init; } = string.Empty;
}
