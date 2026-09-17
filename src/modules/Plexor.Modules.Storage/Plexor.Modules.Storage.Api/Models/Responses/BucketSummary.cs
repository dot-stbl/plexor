// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BucketSummary — wire projection of Bucket for list + detail
// endpoints. Compact — no timestamps; the admin table view doesn't
// need them.
// ============================================================================

namespace Plexor.Modules.Storage.Api.Models.Responses;

/// <summary>
///     Compact wire projection of
///     <c>Plexor.Modules.Storage.Domain.Entities.Bucket</c>. Includes
///     the live book-keeping totals (<see cref="SizeBytes" /> +
///     <see cref="ObjectCount" />) which the NodeAgent reports
///     out-of-band as the bucket fills.
/// </summary>
public sealed class BucketSummary
{
    /// <summary>Bucket id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>S3-style bucket name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Region label.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Cumulative bucket size in bytes (best-effort).</summary>
    public long SizeBytes { get; init; }

    /// <summary>Live object count (best-effort).</summary>
    public long ObjectCount { get; init; }
}
