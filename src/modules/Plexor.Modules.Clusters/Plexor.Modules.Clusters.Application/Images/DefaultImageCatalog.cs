// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DefaultImageCatalog — v0.1 hard-coded image list. One entry: the
// Ubuntu 22.04 LTS cloud image (amd64, server) from the canonical
// cloud-images mirror. The NodeAgent's HttpImageRegistry caches
// the .img on first request; subsequent clones reuse the cached
// file.
// ============================================================================

namespace Plexor.Modules.Clusters.Application.Images;

/// <summary>
///     v0.1 <see cref="IImageCatalog" /> implementation. Hard-coded
///     — adding an image is a code change, reviewed like the rest
///     of the API surface. v0.2+ may swap to a DB-backed admin
///     catalog; the interface doesn't change.
/// </summary>
public sealed class DefaultImageCatalog : IImageCatalog
{
    /// <summary>
    ///     Canonical Ubuntu 22.04 LTS cloud image (amd64, server
    ///     variant). Tag taxonomy: distro, family, era, target
    ///     arch. v0.1 ships exactly one image; multi-image
    ///     catalogs land in v0.2.
    /// </summary>
    private static readonly ImageRef Ubuntu2204 = new(
        Name: "ubuntu-22.04-cloud",
        Source: "https://cloud-images.ubuntu.com/jammy/current/jammy-server-cloudimg-amd64.img",
        Tags: ["ubuntu", "lts", "jammy", "cloud", "amd64"]);

    /// <summary>
    ///     Cached, single-element list — avoids per-call
    ///     allocation in the request hot path.
    /// </summary>
    private static readonly IReadOnlyList<ImageRef> Images = [Ubuntu2204];

    /// <inheritdoc />
    public IReadOnlyList<ImageRef> List(IReadOnlyList<string>? tags = null)
    {
        if (tags is null || tags.Count == 0)
        {
            return Images;
        }

        var filtered = new List<ImageRef>(Images.Count);
        foreach (var image in Images)
        {
            if (DefaultImageCatalogHelpers.TagsContainAll(image.Tags, tags))
            {
                filtered.Add(image);
            }
        }

        return filtered;
    }

    /// <inheritdoc />
    public ImageRef? Get(string name)
    {
        foreach (var image in Images)
        {
            if (string.Equals(image.Name, name, StringComparison.Ordinal))
            {
                return image;
            }
        }

        return null;
    }
}

/// <summary>
///     Pure-function helpers for <see cref="DefaultImageCatalog" />.
///     File-scoped per the class-decomposition rule (no
///     <c>private static</c> on production classes — pure logic
///     lives in a <c>file static class</c> next to the consumer).
/// </summary>
file static class DefaultImageCatalogHelpers
{
    /// <summary>
    ///     True when <paramref name="imageTags" /> contains every
    ///     tag in <paramref name="filter" /> (logical AND). Used by
    ///     <see cref="DefaultImageCatalog.List" /> to intersect the
    ///     catalog with the caller's filter.
    /// </summary>
    /// <param name="imageTags">Tags carried by the image entry.</param>
    /// <param name="filter">Tags the caller asked for (null/empty = no filter).</param>
    public static bool TagsContainAll(IReadOnlyList<string> imageTags, IReadOnlyList<string> filter)
    {
        foreach (var required in filter)
        {
            var found = false;
            foreach (var candidate in imageTags)
            {
                if (string.Equals(candidate, required, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
