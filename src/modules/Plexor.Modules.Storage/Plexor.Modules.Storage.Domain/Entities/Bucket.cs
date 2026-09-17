// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Bucket — S3-compatible object-store bucket. The schema is `storage`
// (architecture theme); the table is `storage.buckets`. One row per
// bucket.
//
// Buckets are NOT quota-affecting in v0.1 — the storage.volumes.count
// and storage.volumes.gb keys target disks, not buckets. Bucket
// rows exist as persistent identity for the bucket itself (name +
// region + size bookkeeping) but they do not consume the per-org
// volume quota. A future "storage.buckets.count" key can land when
// the operator exposes it; no quota wiring on this entity today.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Storage.Domain.Entities;

/// <summary>
///     S3-compatible object-store bucket. Backed by the
///     <c>storage.buckets</c> table. Tenant-scoped via
///     <see cref="OrgId" />.
/// </summary>
/// <remarks>
///     <para><b>One row per bucket.</b> The bucket name + region tuple
///     is globally unique — enforced by the
///     <c>ix_storage_buckets_name_region</c> UNIQUE index. Region is
///     stored as a string (matching Plexor.Modules.Clusters.Domain.Cluster.Region)
///     so a future region addition does not require a schema migration.</para>
///     <para><b>Object counters.</b> <see cref="SizeBytes" /> and
///     <see cref="ObjectCount" /> are best-effort book-keeping the
///     NodeAgent updates out-of-band; they are NOT consumed by the
///     quota enforcer in v0.1.</para>
/// </remarks>
public sealed class Bucket : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope. Denormalized for tenant-scoped queries.
    /// The bucket is owned by one org.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Globally unique bucket name (the S3-style DNS label).
    /// Combined with <see cref="Region" /> it forms the unique
    /// identifier (UNIQUE constraint).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Region the bucket lives in (e.g. <c>"eu-central-1"</c>).
    /// Strings, not an enum, to allow operator-defined region names.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Object store cumulative size in bytes (best-effort,
    /// updated by NodeAgent after each PUT/DELETE batch).</summary>
    public long SizeBytes { get; init; }

    /// <summary>Live object count (best-effort, same cadence as
    /// <see cref="SizeBytes" />).</summary>
    public long ObjectCount { get; init; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC) — bumped on any
    /// field write (size delta, rename, region change).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
