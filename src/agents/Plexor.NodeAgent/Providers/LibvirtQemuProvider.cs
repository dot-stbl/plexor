// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtQemuProvider — IWorkloadProvider impl for QEMU VMs
// running WITHOUT KVM acceleration (TCG software emulation).
// Uses the `qemu:///system` libvirt URI; the domain type is
// `qemu` (not `kvm`) and the machine is a generic `pc` rather
// than the KVM-optimized `pc-i440fx`.
//
// Why a separate Kind? Two reasons:
//   1. KVM needs the KVM kernel module + hardware virtualization
//      (VT-x / AMD-V). Some hosts (older CPUs, locked-down
//      cloud VMs, ARM without virt) don't have it. TCG runs
//      anywhere QEMU runs.
//   2. TCG is significantly slower than KVM (5-10x) and
//      lacks some features (no nested virt, no hugepages on
//      all arches). Surfaces as a different Kind so the UI
//      can show 'qemu (slow)' vs 'kvm (native)'.
//
// Tier 3.5: consumes the same IVolumeBackend + INetworkBackend
// abstractions as the KVM provider — just with a different
// domain type + machine type in the XML. The pure-function
// XML builder lives in LibvirtQemuXmlBuilder.cs (extracted
// when this file crossed 300 lines; see class-decomposition.md).
// ==========================================================================

using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.Compute;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers;

/// <summary>
///     <see cref="IWorkloadProvider" /> for QEMU VMs without KVM
///     acceleration. Same wire format as <see cref="LibvirtKvmProvider" />
///     (same XML builder, just a different <c>type</c> +
///     <c>machine</c> attribute) but a different
///     <see cref="WorkloadKind" /> so the agent's dispatcher routes
///     the right commands to the right backend.
/// </summary>
/// <param name="volumes">Storage backend — supplies the qcow2 disk image the VM boots from.</param>
/// <param name="networks">Network topology backend — supplies the bridge the VM's NIC attaches to.</param>
/// <param name="logger"></param>
public sealed class LibvirtQemuProvider(
    IVolumeBackend volumes,
    INetworkBackend networks,
    ILogger<LibvirtQemuProvider> logger) : IWorkloadProvider
{
    /// <summary>
    ///     The libvirt URI for QEMU on the local system.
    ///     Same URI as KVM (qemu:///system); what differs is the
    ///     domain type (qemu vs kvm) and the machine type
    ///     (pc-generic vs pc-i440fx).
    /// </summary>
    public static readonly Uri LibvirtUri = new("qemu:///system");

    private readonly WorkloadIdMap workloads = new();

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.Qemu();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(WorkloadSpec spec, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();

        // Tier 3.5: QEMU consumes the same IVolumeBackend +
        // INetworkBackend abstractions as KVM. The volume is a
        // qcow2 file (QEMU's standard disk format); the network is
        // a Linux bridge. Both handles are recorded in
        // WorkloadIdMap so DeleteAsync can free them on teardown.
        var volumeSpec = new VolumeSpec(
            Name: spec.Name,
            SizeBytes: 4L * 1024L * 1024L * 1024L,
            BaseImageRef: null,
            Format: VolumeFormat.Qcow2);
        var volumeHandle = await volumes.CreateAsync(volumeSpec, cancellationToken);

        var networkSpec = new NetworkSpec(spec.Name, NetworkKind.LinuxBridge);
        var networkHandle = await networks.AttachAsync(networkSpec, cancellationToken);

        var xml = LibvirtQemuXmlBuilder.BuildDomainXml(
            spec,
            id,
            volumePath: volumeHandle.Reference,
            networkBridge: networkHandle.Reference);
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
                    "Best-effort cleanup of {Domain} after failed create failed",
                    spec.Name);
            }

            // Tier 3.5: best-effort cleanup of the volume + network
            // we allocated. The backends are idempotent — missing
            // handle is a successful no-op.
            try { await volumes.DeleteAsync(volumeHandle, CancellationToken.None); }
            catch (Exception cleanup) { logger.LogWarning(cleanup, "Best-effort cleanup of volume {Volume} after failed create failed", volumeHandle.Reference); }
            try { await networks.DetachAsync(networkHandle, CancellationToken.None); }
            catch (Exception cleanup) { logger.LogWarning(cleanup, "Best-effort cleanup of network {Network} after failed create failed", networkHandle.Reference); }

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

        workloads.Register(id, spec.Name, Kind, volumeHandle, networkHandle);
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

        // Best-effort undefine — partial failures shouldn't block
        // the volume / network release (operator can `virsh
        // undefine` manually if needed).
        try
        {
            await LibvirtRunner.RunAsync(LibvirtUri, $"undefine {entry.DomainName}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "LibvirtQemuProvider: virsh undefine {Domain} failed during delete; "
                + "continuing with volume / network cleanup",
                entry.DomainName);
        }

        // Tier 3.5: free the volume + network we allocated at
        // create-time. Both backends are idempotent.
        await volumes.DeleteAsync(entry.VolumeHandle, cancellationToken);
        await networks.DetachAsync(entry.NetworkHandle, cancellationToken);

        if (!workloads.Remove(id))
        {
            throw new InvalidOperationException(
                $"LibvirtQemuProvider: race removing workload {id}.");
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
