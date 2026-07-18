// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LocalDirImageRegistry — IImageRegistry reference impl. Operator
// pre-populates a directory with base images (Ubuntu cloud
// image, Plexor firewall, etc.) and registers their filenames
// via NodeAgent config; the registry returns the cached path on
// every call.
//
// The catalog is static (configuration-only) — adding/removing
// images requires restarting the NodeAgent. That's fine for
// v0.1: images change rarely, and the registry stays simple.
//
// Future v0.2+: HttpImageRegistry downloads images on demand
// from a configured URL. The interface is the same — switch by
// changing one DI registration in Storage/ImageInstaller.cs.
// ==========================================================================

using Microsoft.Extensions.Options;
using Plexor.Shared.Compute;

namespace Plexor.NodeAgent.Providers.Image;

/// <summary>
///     Static image registry. Catalog is loaded from
///     <see cref="ImageRegistryOptions" /> at startup; the
///     directory is queried on first request, then cached in-
///     memory.
/// </summary>
/// <param name="options">
///     Bound from configuration
///     (<c>NodeAgent:Images:&lt;name&gt;</c>).
/// </param>
public sealed class LocalDirImageRegistry(IOptions<ImageRegistryOptions> options) : IImageRegistry
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> AvailableImages =>
        options.Value.Catalog.Keys.ToArray();

    /// <inheritdoc />
    public Task<string> EnsureLocalAsync(string imageRef, CancellationToken cancellationToken)
    {
        if (!options.Value.Catalog.TryGetValue(imageRef, out var relativePath))
        {
            throw new UnknownImageException(imageRef);
        }

        // Resolve against the operator-configured root directory.
        // No filesystem check — the operator is responsible for
        // pre-populating the directory. qemu-img will fail
        // loudly on CreateAsync if the base path is missing.
        // Concatenate with forward-slash explicitly. Plexor's
        // storage model is Linux-targeted — operators provision
        // base images on ext4 / xfs / ceph-fs mounts, never on
        // Windows. Using Path.Combine here would produce
        // platform-specific separators (`\` on Windows) that don't
        // match the Linux paths qemu-img / libvirt expect.
        var fullPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : $"{options.Value.RootDirectory!.TrimEnd('/')}/{relativePath.TrimStart('/')}";

        return Task.FromResult(fullPath);
    }
}

/// <summary>
///     Configuration for <see cref="LocalDirImageRegistry" />.
///     Bound from <c>NodeAgent:Images</c> at startup; the
///     dictionary maps operator-facing image refs (e.g.
///     <c>"ubuntu-22.04-cloud"</c>) to filenames inside
///     <see cref="RootDirectory" /> (or absolute paths).
/// </summary>
/// <param name="RootDirectory">
///     Absolute path to the directory holding base image files
///     (qcow2/raw). When null, image refs in
///     <see cref="Catalog" /> must be absolute paths.
/// </param>
/// <param name="Catalog">
///     Map of image ref → relative path under
///     <see cref="RootDirectory" />. Empty catalog = registry
///     reports zero available images.
/// </param>
public sealed record ImageRegistryOptions(
    string? RootDirectory,
    IReadOnlyDictionary<string, string> Catalog)
{
    /// <summary>
    ///     Empty catalog used when no <c>NodeAgent:Images</c>
    ///     section is configured. Every ref lookup throws
    ///     <see cref="UnknownImageException" />.
    /// </summary>
    public static ImageRegistryOptions Empty { get; } =
        new(RootDirectory: null, Catalog: new Dictionary<string, string>());
}
