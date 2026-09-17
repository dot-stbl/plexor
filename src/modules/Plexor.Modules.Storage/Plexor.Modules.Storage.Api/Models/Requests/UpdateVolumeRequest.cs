// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpdateVolumeRequest — wire shape for PUT /api/v1/storage/volumes/{id}.
// Currently only resizing (SizeGb) is supported; Status changes
// (Pending → Attached etc.) land when the NodeAgent runtime
// implements the attach dance.
// ============================================================================

namespace Plexor.Modules.Storage.Api.Models.Requests;

/// <summary>
///     Wire shape for the update-volume request body. Only the fields
///     a v0.1 admin can mutate are exposed — Status changes land when
///     the NodeAgent runtime implements the attach dance.
/// </summary>
public sealed class UpdateVolumeRequest
{
    /// <summary>New volume size in GiB. Triggers the
    /// <c>storage.volumes.gb</c> quota re-check (the caller is
    /// charged for the delta, not the new total).</summary>
    public int SizeGb { get; init; }
}
