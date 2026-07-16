// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LocalDirStorage — IVolumeBackend reference impl that keeps
// volumes on the node's local filesystem under a configurable
// root directory. v0.1's only backend; multi-node deployments
// will add CephRbdStorage on top of this.
//
// Volume lifecycle:
//   - CreateAsync: qemu-img create -f qcow2 -b {baseImage} -F qcow2
//     {root}/{name}.qcow2 {size}G  (overlay qcow2 with base
//     backing file when BaseImageRef is supplied; raw blank file
//     otherwise).
//   - DeleteAsync: rm -f {path}. Idempotent — missing file is OK.
//
// The VolumeHandle.Reference is the absolute path on the node's
// local filesystem. LibvirtKvmProvider passes that path to
// <source file="..."/> in the domain XML.
// ==========================================================================

using Plexor.Shared.Compute;

namespace Plexor.NodeAgent.Providers.Storage;

/// <summary>
///     Single-node <see cref="IVolumeBackend" /> that keeps
///     volumes as qcow2 files on the local filesystem.
/// </summary>
/// <param name="root">
///     Absolute path to the directory under which volumes are
///     stored. The directory must exist; the constructor does
///     not create it (operator pre-provisions).
/// </param>
/// <param name="imageRegistry">
///     Resolves <see cref="VolumeSpec.BaseImageRef" /> to a
///     cached base-image path. The backend clones from that
///     path via <c>qemu-img create -b</c>.
/// </param>
/// <param name="logger"></param>
public sealed class LocalDirStorage(
    string root,
    IImageRegistry imageRegistry,
    ILogger<LocalDirStorage> logger) : IVolumeBackend
{
    /// <summary>Stable backend name — used in <see cref="VolumeHandle.BackendName" />.</summary>
    public const string BackendName = "local-dir";

    /// <inheritdoc />
    public async Task<VolumeHandle> CreateAsync(VolumeSpec volumeSpec, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, $"{volumeSpec.Name}.{volumeSpec.Format.ToString().ToLowerInvariant()}");

        var sizeArg = $"{volumeSpec.SizeBytes / (1024L * 1024L * 1024L)}G";

        if (volumeSpec.BaseImageRef is null)
        {
            // Blank disk — no backing image. qemu-img create -f
            // qcow2 {path} {size}G. Sparse by default.
            await QemuImageRunner.RunAsync(
                $"create -f {volumeSpec.Format.ToString().ToLowerInvariant()} \"{path}\" {sizeArg}",
                cancellationToken);
        }
        else
        {
            // Clone from base image. -F qcow2 keeps the backing
            // format explicit so qemu-img doesn't have to probe.
            var basePath = await imageRegistry.EnsureLocalAsync(volumeSpec.BaseImageRef, cancellationToken);
            await QemuImageRunner.RunAsync(
                $"create -f {volumeSpec.Format.ToString().ToLowerInvariant()} "
                + $"-b \"{basePath}\" -F qcow2 \"{path}\" {sizeArg}",
                cancellationToken);
        }

        logger.LogInformation(
            "LocalDirStorage: created volume {Name} at {Path} ({Size}, base={Base})",
            volumeSpec.Name,
            path,
            sizeArg,
            volumeSpec.BaseImageRef ?? "<none>");

        return new VolumeHandle(BackendName, path);
    }

    /// <inheritdoc />
    public Task DeleteAsync(VolumeHandle handle, CancellationToken cancellationToken)
    {
        if (handle.BackendName != BackendName)
        {
            throw new ArgumentException(
                $"VolumeHandle was issued by backend '{handle.BackendName}', "
                + $"not '{BackendName}'.",
                nameof(handle));
        }

        // Idempotent — missing file is OK. rm -f doesn't error
        // on a non-existent path.
        try
        {
            File.Delete(handle.Reference);
            logger.LogInformation(
                "LocalDirStorage: deleted volume {Path}",
                handle.Reference);
        }
        catch (DirectoryNotFoundException)
        {
            // Same idempotency semantics — directory disappeared,
            // treat as success.
        }

        return Task.CompletedTask;
    }
}
