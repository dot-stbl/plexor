// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ImageRef — a VM base image the operator can pick. The catalog
// serves a small set of well-known cloud images; the NodeAgent's
// IImageRegistry handles the actual download + cache. The control
// plane only needs to know the name and source URL — the agent
// resolves it locally.
// ============================================================================

namespace Plexor.Modules.Clusters.Application.Images;

/// <summary>
///     Reference to a base image the operator can pick for a VM.
///     Wire-stable shape: the <see cref="Name" /> is what the
///     operator picks from; the <see cref="Source" /> is what the
///     NodeAgent's <c>IImageRegistry</c> downloads on first use.
/// </summary>
/// <param name="Name">
///     Catalog id (e.g. <c>"ubuntu-22.04-cloud"</c>). Unique per
///     <see cref="IImageCatalog" /> instance. Forwarded to the
///     NodeAgent as <c>VmRuntimeConfig.ImageRef</c>.
/// </param>
/// <param name="Source">
///     Where the NodeAgent downloads the image from on first use
///     (e.g. <c>https://cloud-images.ubuntu.com/jammy/current/
///     jammy-server-cloudimg-amd64.img</c>). Stored verbatim —
///     the agent's image registry interprets the URL.
/// </param>
/// <param name="Tags">
///     Free-form labels for filtering (e.g. <c>"ubuntu"</c>,
///     <c>"cloud"</c>, <c>"lts"</c>). v0.1 only filters by exact
///     tag match; tag taxonomy is up to the operator.
/// </param>
public sealed record ImageRef(
    string Name,
    string Source,
    IReadOnlyList<string> Tags);
