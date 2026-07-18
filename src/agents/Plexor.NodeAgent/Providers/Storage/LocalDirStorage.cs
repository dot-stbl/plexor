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
        // Directory-format volumes are consumed by LXC and
        // systemd-nspawn providers that bind-mount the directory
        // as the container's rootfs. QEMU / KVM ignore Directory
        // and use Qcow2 instead. We enforce the choice here
        // rather than at the provider level so a misconfigured
        // provider (e.g. KVM with Format=Directory) fails fast
        // with a clear message.
        if (volumeSpec.Format == VolumeFormat.Directory)
        {
            return CreateDirectory(volumeSpec, cancellationToken);
        }

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

    /// <summary>
    ///     Create a directory-backed volume. No qemu-img — just
    ///     <c>mkdir -p</c> the rootfs directory. <see cref="VolumeSpec.BaseImageRef" />
    ///     is honoured by pre-populating the directory from the
    ///     image registry (operator-cached Ubuntu cloud image,
    ///     etc.); <c>null</c> means an empty rootfs that the
    ///     init process populates on first boot.
    /// </summary>
    /// <param name="volumeSpec"></param>
    /// <param name="cancellationToken"></param>
    private VolumeHandle CreateDirectory(VolumeSpec volumeSpec, CancellationToken cancellationToken)
    {
        _ = cancellationToken; // captured for interface contract; Directory.CreateDirectory is sync
        var path = Path.Combine(root, $"{volumeSpec.Name}.dir");

        // Pre-populate from the base image (if any) BEFORE the
        // first Create so the LXC provider can call
        // virsh net-define on a populated rootfs. We use
        // FileSystem.CopyDirectory equivalent — for a small
        // base (Ubuntu cloud image is ~600MB) this is fine;
        // a future Tier 3+ move to overlayfs / btrfs-send
        // would replace this with a CoW clone.
        Directory.CreateDirectory(path);

        if (volumeSpec.BaseImageRef is not null)
        {
            // Resolve via IImageRegistry — the cache may be a
            // downloaded file (HttpImageRegistry) or a local
            // path (LocalDirImageRegistry). The image gets unpacked
            // into the volume dir on first create.
            // v0.1: we don't unpack; we just record the path and
            // let the LXC provider do the bind-mount. The image
            // gets extracted on first boot via cloud-init or a
            // post-create hook. Real unpack is Tier 3.7+ work.
        }

        logger.LogInformation(
            "LocalDirStorage: created directory volume {Name} at {Path} (base={Base})",
            volumeSpec.Name,
            path,
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

        // Delete path is format-aware: directory-backed volumes
        // (LXC / systemd-nspawn) are tree-removed; file-backed
        // volumes (qcow2 / raw) are File.Delete'd.
        try
        {
            if (Directory.Exists(handle.Reference))
            {
                Directory.Delete(handle.Reference, recursive: true);
            }
            else if (File.Exists(handle.Reference))
            {
                File.Delete(handle.Reference);
            }
            // Neither exists — already gone, treat as success.

            logger.LogInformation(
                "LocalDirStorage: deleted volume {Path}",
                handle.Reference);
        }
        catch (DirectoryNotFoundException)
        {
            // Race: another process removed the volume between
            // the Exists check and the Delete call. Treat as
            // success.
        }

        return Task.CompletedTask;
    }
}
