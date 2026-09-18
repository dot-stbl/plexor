// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateBucketRequest — wire shape for POST /api/v1/storage/buckets.
// ============================================================================

namespace Plexor.Modules.Storage.Api.Models.Requests;

/// <summary>
///     Wire shape for the create-bucket request body. Name + Region are
///     required; SizeBytes + ObjectCount default to 0 (the NodeAgent
///     reports the cumulative totals out-of-band as the bucket fills).
/// </summary>
public sealed class CreateBucketRequest
{
    /// <summary>S3-style bucket name (DNS label, ≤64 chars). Combined
    /// with <see cref="Region" /> to form the global unique
    /// identifier (UNIQUE on (name, region)).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Region label (e.g. <c>"eu-central-1"</c>, ≤64 chars).</summary>
    public string Region { get; init; } = string.Empty;
}
