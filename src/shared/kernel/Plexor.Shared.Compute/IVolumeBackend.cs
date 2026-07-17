// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IVolumeBackend — storage abstraction. A volume is a disk image
// the VM provider attaches as the boot disk (or a secondary data
// disk). The backend decides HOW the volume lives (qcow2 file on
// local fs, RBD image in a Ceph pool, NFS-backed raw file, S3-
// streamed raw) and exposes a backend-specific handle that the VM
// provider references in its domain XML's <source> element.
//
// One backend per storage technology. The NodeAgent registers the
// backends it has configured (e.g. only LocalDirStorage on a
// single-node box; LocalDirStorage + CephRbdStorage on a multi-
// node cluster). The IWorkloadProvider asks DI for the backend
// matching the requested VolumeSpec and ignores the rest.
// ==========================================================================

namespace Plexor.Shared.Compute;

/// <summary>
///     Per-node storage backend. A backend owns the lifetime of
///     the volumes it creates — the caller (workload provider)
///     only sees the opaque <see cref="VolumeHandle" /> and asks
///     the backend to delete it.
/// </summary>
public interface IVolumeBackend
{
    /// <summary>
    ///     Provision a volume for the given workload. May clone
    ///     from <see cref="VolumeSpec.BaseImageRef" /> via the
    ///     registry, or create a blank disk if no base image is
    ///     supplied.
    /// </summary>
    /// <param name="volumeSpec">
    ///     Size, format, optional base image ref. The backend
    ///     interprets <see cref="VolumeSpec.SizeBytes" /> as the
    ///     "virtual" size; physical allocation is backend-defined
    ///     (sparse for qcow2, full for raw, RBD pool-defined for
    ///     Ceph).
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Opaque handle the workload provider passes back when
    ///     it wants the volume deleted. For LocalDirStorage this
    ///     is an absolute path; for CephRbdStorage it's a
    ///     <c>pool/image</c> string; for S3 it might be a URL +
    ///     etag. The wire representation is backend-defined — the
    ///     VM provider does not interpret it, only the backend
    ///     that issued it does.
    /// </returns>
    public Task<VolumeHandle> CreateAsync(VolumeSpec volumeSpec, CancellationToken cancellationToken);

    /// <summary>
    ///     Free the volume. Idempotent — deleting a missing
    ///     volume is a successful no-op (matches the "delete is
    ///     eventual" semantics the VM provider expects when the
    ///     control plane issues a delete on a workload that was
    ///     already torn down).
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="cancellationToken"></param>
    public Task DeleteAsync(VolumeHandle handle, CancellationToken cancellationToken);
}

/// <summary>
///     Spec for a single volume. The backend interprets the fields
///     it understands and ignores the rest (so adding a new field
///     here doesn't break older backends).
/// </summary>
/// <param name="Name">
///     Logical name of the volume (matches
///     <c>WorkloadSpec.Name</c> — used as the basis for the
///     backend's storage identifier, e.g. filename for LocalDir,
///     RBD image name for CephRbd).
/// </param>
/// <param name="SizeBytes">Virtual size in bytes.</param>
/// <param name="BaseImageRef">
///     Operator-facing image ref resolved through
///     <see cref="IImageRegistry" />. Null = blank disk.
/// </param>
/// <param name="Format">Disk image format the backend should produce.</param>
public sealed record VolumeSpec(
    string Name,
    long SizeBytes,
    string? BaseImageRef,
    VolumeFormat Format);

/// <summary>
///     Disk image formats the backend may produce. The set is
///     closed — adding a format is a contract change.
/// </summary>
public enum VolumeFormat
{
    /// <summary>qcow2 — qemu copy-on-write, sparse by default.</summary>
    Qcow2 = 0,

    /// <summary>raw — fixed-size, no metadata.</summary>
    Raw = 1,

    /// <summary>
    ///     Ceph RBD image. The backend manages the rbd pool;
    ///     <see cref="VolumeHandle.Reference" /> carries the
    ///     <c>pool/image</c> name the libvirt domain references
    ///     via the rbd secret.
    /// </summary>
    Rbd = 2,

    /// <summary>
    ///     Plain directory on the host filesystem. The backend
    ///     ensures the directory exists (and is empty for a
    ///     blank-disk request, or pre-populated when
    ///     <see cref="VolumeSpec.BaseImageRef" /> is supplied).
    ///     Used by LXC / systemd-nspawn providers that bind-mount
    ///     the directory as the container's rootfs. QCOW2 file-
    ///     based providers ignore this format — they require
    ///     <see cref="Qcow2" /> instead.
    /// </summary>
    Directory = 3
}

/// <summary>
///     Opaque storage handle issued by an
///     <see cref="IVolumeBackend" />. The backend name plus the
///     backend-specific reference identify the volume uniquely.
///     </summary>
/// <param name="BackendName">
///     Stable name of the backend that issued the handle
///     (e.g. <c>"local-dir"</c>, <c>"ceph-rbd"</c>). Used for
///     diagnostics and for routing back to the right backend on
///     <see cref="IVolumeBackend.DeleteAsync" />.
/// </param>
/// <param name="Reference">
///     Backend-specific identifier. For LocalDirStorage this is an
///     absolute filesystem path; for CephRbdStorage this is a
///     <c>pool/image</c> string. The VM provider does not parse
///     this — it only stores it on the workload and passes it
///     back to the backend on delete.
/// </param>
public sealed record VolumeHandle(string BackendName, string Reference);
