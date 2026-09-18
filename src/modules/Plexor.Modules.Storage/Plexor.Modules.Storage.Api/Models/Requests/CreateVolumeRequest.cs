// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVolumeRequest — wire shape for POST /api/v1/storage/volumes.
// Validated by CreateVolumeRequestValidator. The ClusterId is part of
// the request because the API layer resolves the cluster independently
// (storage module does not FK into Clusters).
// ============================================================================

namespace Plexor.Modules.Storage.Api.Models.Requests;

/// <summary>
///     Wire shape for the create-volume request body. ClusterId +
///     Name + SizeGb are required; Status defaults to Pending.
/// </summary>
public sealed class CreateVolumeRequest
{
    /// <summary>Target cluster id. The API layer validates the
    /// cluster exists and belongs to the caller's org before
    /// INSERT — the storage module does not FK into Plexor.Modules.Clusters.</summary>
    public Guid ClusterId { get; init; }

    /// <summary>Volume name (1-128 chars). Unique per cluster
    /// (enforced by the (cluster_id, name) UNIQUE index).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Volume size in GiB. Drives the
    /// <c>storage.volumes.gb</c> quota counter.</summary>
    public int SizeGb { get; init; }
}
