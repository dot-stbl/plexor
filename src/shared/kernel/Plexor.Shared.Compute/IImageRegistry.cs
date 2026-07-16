// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IImageRegistry — catalog of base images the NodeAgent can clone
// into volumes. Sits BELOW IVolumeBackend: the volume backend asks
// the registry for a ready-to-clone image path; the volume backend
// then creates the per-workload overlay/clone on whatever storage
// it owns.
//
// A registry implementation may:
//   - Download from an HTTP/OCI source on first request and cache
//     on local disk (HttpImageRegistry).
//   - Read from a pre-populated local directory operator-managed
//     out-of-band (LocalDirImageRegistry).
//   - Pull from a content-addressable store (Ceph RBD image cache,
//     S3-backed image registry).
//
// The registry never holds or exposes the per-workload storage —
// that's the volume backend's job. The registry only knows how to
// produce a base image; the volume backend decides where the clone
// lives.
// ==========================================================================

namespace Plexor.Shared.Compute;

/// <summary>
///     Per-node catalog of base images available for cloning. The
///     registry caches the image on local disk the first time it's
///     requested; subsequent calls return the cached path.
/// </summary>
public interface IImageRegistry
{
    /// <summary>
    ///     Resolve <paramref name="imageRef" /> to a local
    ///     filesystem path that <see cref="IVolumeBackend" />
    ///     can clone from. Downloads from the registry's source
    ///     on first call; idempotent on subsequent calls.
    /// </summary>
    /// <param name="imageRef">
    ///     Operator-facing image identifier (e.g.
    ///     <c>"ubuntu-22.04-cloud"</c>, <c>"plexor-fw-1.0"</c>).
    ///     Throws <see cref="UnknownImageException" /> when the
    ///     registry has no record of this ref.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Absolute path to the cached base image. Caller treats
    ///     this as read-only — the registry may invalidate it
    ///     between calls.
    /// </returns>
    /// <exception cref="UnknownImageException"></exception>
    public Task<string> EnsureLocalAsync(string imageRef, CancellationToken cancellationToken);

    /// <summary>
    ///     Image refs this registry knows about. Used by the
    ///     control plane to surface available choices in the
    ///     workload create flow. Order is implementation-defined
    ///     (typically alphabetical or catalog-source-order).
    /// </summary>
    public IReadOnlyCollection<string> AvailableImages { get; }
}

/// <summary>
///     Thrown by <see cref="IImageRegistry.EnsureLocalAsync" />
///     when the registry has no record of the requested image
///     ref. The control plane surfaces this as 400 Bad Request
///     with the registry's <see cref="IImageRegistry.AvailableImages" />
///     list so the user picks from the valid set.
/// </summary>
/// <param name="ImageRef">Image ref that wasn't found.</param>
public sealed class UnknownImageException(string ImageRef)
    : Exception($"Image '{ImageRef}' is not in any registered image registry.")
{
    /// <summary>Image ref that wasn't found.</summary>
    public string ImageRef { get; } = ImageRef;
}
