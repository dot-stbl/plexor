// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtLxcProvider — IWorkloadProvider impl for LXC system
// containers via libvirt. Uses the `lxc:///system` libvirt URI;
// the agent's host must have libvirt's lxc driver installed
// (RHEL/Fedora: libvirt-daemon-driver-storage-core +
// libvirt-daemon-driver-lxc; Debian/Ubuntu: libvirt-daemon-driver-lxc).
//
// LXC vs KVM: no machine type, no BIOS/UEFI, no firmware boot.
// LXC runs an init process inside a chroot/namespace; the
// domain XML is fundamentally different (no <os type='hvm'>, no
// <disk>, the <os> has <init> instead).
//
// v0.1 simplifications:
//   - No template image (every container starts from an empty
//     rootfs); real impl supports a 'template' config key that
//     names a base image to clone on create.
//   - The rootfs is mounted read-write; production wants
//     overlayfs on top of a base image.
//   - No cgroup limits beyond the basic <memory> + <vcpu>;
//     production parses the rest of the spec.Config.
// ============================================================================

using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.Compute;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers;

/// <summary>
///     <see cref="IWorkloadProvider" /> for LXC system containers via
///     libvirt. Different <see cref="WorkloadKind" /> from KVM (the
///     agent's dispatcher routes by Kind), so the agent runs the
///     same commands against fundamentally different technology.
/// </summary>
/// <param name="volumes"></param>
/// <param name="networks"></param>
/// <param name="logger"></param>
/// <remarks>
///     Build a provider that talks to the local libvirt
///     LXC driver.
/// </remarks>
public sealed class LibvirtLxcProvider(
    IVolumeBackend volumes,
    INetworkBackend networks,
    ILogger<LibvirtLxcProvider> logger) : IWorkloadProvider
{
    /// <summary>
    ///     The libvirt URI for the local LXC driver. v0.1
    ///     hardcodes this; v0.2+ reads it from configuration.
    /// </summary>
    public static readonly Uri LibvirtUri = new("lxc:///system");

    private readonly WorkloadIdMap workloads = new();

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.Lxc();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(WorkloadSpec spec, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();

        // Tier 3.5: LXC consumes the same IVolumeBackend +
        // INetworkBackend abstractions as KVM. The format is
        // Directory (a host bind-mount source), not Qcow2 —
        // LocalDirStorage.CreateDirectory handles that path.
        var volumeSpec = new VolumeSpec(
            Name: spec.Name,
            SizeBytes: 0,
            BaseImageRef: LibvirtLxcXmlBuilder.ResolveBaseImageRef(spec),
            Format: VolumeFormat.Directory);
        var volumeHandle = await volumes.CreateAsync(volumeSpec, cancellationToken);

        var networkSpec = new NetworkSpec(spec.Name, NetworkKind.LinuxBridge);
        var networkHandle = await networks.AttachAsync(networkSpec, cancellationToken);

        var rootfs = volumeHandle.Reference;
        var xml = LibvirtLxcXmlBuilder.BuildContainerXml(spec, id, rootfs, networkHandle.Reference);
        var xmlPath = $"/tmp/plexor-{id}.xml";

        try
        {
            await File.WriteAllTextAsync(xmlPath, xml, cancellationToken);
            await LibvirtRunner.RunAsync(LibvirtUri, $"define {xmlPath}", cancellationToken);
            await LibvirtRunner.RunAsync(LibvirtUri, $"start {spec.Name}", cancellationToken);
        }
        catch
        {
            try
            {
                await LibvirtRunner.RunAsync(LibvirtUri, $"undefine {spec.Name}", CancellationToken.None);
            }
            catch (Exception cleanup)
            {
                logger.LogWarning(
                    cleanup,
                    "Best-effort cleanup of {Container} after failed create failed",
                    spec.Name);
            }

            throw;
        }
        finally
        {
            try
            {
                File.Delete(xmlPath);
            }
            catch (Exception cleanup)
            {
                logger.LogDebug(
                    cleanup,
                    "Could not delete temp xml {Path}; leaving in /tmp",
                    xmlPath);
            }
        }

        workloads.Register(
            id,
            spec.Name,
            Kind,
            // Tier 3.5 — LXC provider will adopt IVolumeBackend +
            // INetworkBackend; for now placeholder handles are
            // recorded so the entry is valid.
            new VolumeHandle("legacy", spec.Name),
            new NetworkInterfaceHandle("legacy", "default"));
        return new LocalWorkload(
            id,
            spec.Name,
            Kind,
            WorkloadState.Running,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StartAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        await LibvirtRunner.RunAsync(LibvirtUri, $"start {entry.DomainName}", cancellationToken);
        workloads.SetState(id, WorkloadState.Running);
        return Snapshot(id, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StopAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        await LibvirtRunner.RunAsync(LibvirtUri, $"shutdown {entry.DomainName}", cancellationToken);
        workloads.SetState(id, WorkloadState.Stopped);
        return Snapshot(id, null);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);

        // Undefine the domain. Best-effort cleanup: a partially-
        // failed undefine shouldn't block the volume / network
        // release (the operator can `virsh undefine` manually
        // later if needed).
        try
        {
            await LibvirtRunner.RunAsync(LibvirtUri, $"undefine {entry.DomainName}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "LibvirtLxcProvider: virsh undefine {Domain} failed during delete; "
                + "continuing with volume / network cleanup",
                entry.DomainName);
        }

        // Tier 3.5: free the volume + network we allocated at
        // create-time. Both backends are idempotent — missing
        // handle is a successful no-op (matches the "delete is
        // eventual" semantics the control plane expects).
        await volumes.DeleteAsync(entry.VolumeHandle, cancellationToken);
        await networks.DetachAsync(entry.NetworkHandle, cancellationToken);

        if (!workloads.Remove(id))
        {
            throw new InvalidOperationException(
                $"LibvirtLxcProvider: race removing workload {id}.");
        }

        return new LocalWorkload(
            id,
            entry.DomainName,
            entry.Kind,
            WorkloadState.Stopped,
            DateTimeOffset.UtcNow,
            null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LocalWorkload>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<LocalWorkload>>(
            workloads.Snapshot(Environment.MachineName));
    }

    /// <summary>
    ///     Build a <see cref="LocalWorkload" /> snapshot for
    ///     the given id with the given startedAt timestamp.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="startedAt"></param>
    private LocalWorkload Snapshot(Guid id, DateTimeOffset? startedAt)
    {
        var entry = workloads.GetOrThrow(id);
        return new LocalWorkload(
            id,
            entry.DomainName,
            entry.Kind,
            entry.State,
            DateTimeOffset.UtcNow,
            startedAt);
    }
}

