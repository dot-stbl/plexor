// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IImageCatalog — read-only catalog of base images the operator
// can pick. The v0.1 implementation is a hard-coded list
// (DefaultImageCatalog); v0.2+ may swap to a DB-backed admin-
// managed catalog without touching the call sites.
// ============================================================================

namespace Plexor.Modules.Clusters.Application.Images;

/// <summary>
///     Catalog of <see cref="ImageRef" /> entries the operator can
///     pick for a VM. Registered as a singleton in DI (catalogs
///     are immutable during the process lifetime).
/// </summary>
public interface IImageCatalog
{
    /// <summary>
    ///     List images matching the optional tag filter. When
    ///     <paramref name="tags" /> is null or empty, every image
    ///     in the catalog is returned; otherwise the result is
    ///     the intersection of images that carry every supplied tag
    ///     (logical AND).
    /// </summary>
    /// <param name="tags">
    ///     Optional filter — null / empty = no filter. Otherwise
    ///     every returned image has every supplied tag.
/// </param>
    public IReadOnlyList<ImageRef> List(IReadOnlyList<string>? tags = null);

    /// <summary>
    ///     Look up an image by its <see cref="ImageRef.Name" />.
    ///     Returns null when the catalog has no entry — callers
    ///     surface this as 400 Bad Request with the available
    ///     names listed in the response body.
    /// </summary>
    /// <param name="name">Catalog id to resolve.</param>
    public ImageRef? Get(string name);
}