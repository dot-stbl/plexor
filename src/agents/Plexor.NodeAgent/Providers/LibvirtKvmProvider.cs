// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtKvmProvider — IWorkloadProvider for KVM virtual machines
// via libvirt. v0.1 shells out to the `virsh` CLI (via
// LibvirtRunner); the future v0.2+ impl swaps in LibvirtClient
// for richer async + no shell-quoting footguns.
//
// Compute stack wiring (Tier 3.5):
//   - Volume:  asks IVolumeBackend for a VolumeHandle, then
//     references the handle's Reference in <source>.
//   - Network: asks INetworkBackend for a NetworkInterfaceHandle,
//     references it in <interface>.
//   - Image:   resolved transitively through IVolumeBackend
//     (LocalDirStorage calls IImageRegistry.EnsureLocalAsync
//     to clone from a base image).
//
// Each Plexor workload maps to a single libvirt domain whose
// name is the WorkloadSpec.Name. The provider's local id is
// generated at create-time and tracked in WorkloadIdMap so
// the agent's start/stop/delete calls resolve to the right
// domain.
//
// Domain XML generation + provider config deserialisation live
// in LibvirtKvmXmlBuilder + LibvirtConfigDeserializer (Sprint 3
// item 6 — extracted from this provider's private helpers).
// ==========================================================================

using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.Compute;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers;

/// <summary>
///     <see cref="IWorkloadProvider" /> for KVM VMs via libvirt. v0.1
///     uses the <c>virsh</c> CLI; future v0.2+ uses LibvirtClient.
///
///     Sprint 3 (item 1): wall-clock now read via injected
///     <see cref="TimeProvider" /> per time-and-wire-format.md §3.
/// </summary>
/// <param name="volumes">
///     Storage backend — provides the disk image the domain
///     boots from. Multiple backends may be registered; this
///     provider uses the first one (v0.1 single-backend).
/// </param>
/// <param name="networks">
///     Network topology backend — provides the bridge the
///     domain's NIC attaches to.
/// </param>
/// <param name="logger"></param>
/// <param name="clock"></param>
public sealed class LibvirtKvmProvider(
    IVolumeBackend volumes,
    INetworkBackend networks,
    ILogger<LibvirtKvmProvider> logger,
    TimeProvider clock) : IWorkloadProvider
{
    /// <summary>
    ///     The libvirt URI for KVM/QEMU on the local
    ///     system. v0.1 hardcodes this; v2.2+ reads it from
    ///     configuration so the agent can target remote libvirt
    ///     hosts.
    /// </summary>
    public static readonly Uri LibvirtUri = new("qemu:///system");

    private readonly WorkloadIdMap workloads = new(clock);

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.Vm();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(WorkloadSpec spec, CancellationToken cancellationToken)
    {
        var config = LibvirtConfigDeserializer.TryDeserialize(spec.Config, () => new LibvirtKvmConfig(), out var c)
                ? c
                : new LibvirtKvmConfig();

        var id = Guid.NewGuid();

        // Storage + network through the abstractions. Backend
        // choice happens in DI — v0.1 has exactly one of each.
        var volumeSpec = new VolumeSpec(
            Name: spec.Name,
            SizeBytes: config.RamBytes * 4,
            BaseImageRef: config.BaseImageRef,
            Format: VolumeFormat.Qcow2);
        var volumeHandle = await volumes.CreateAsync(volumeSpec, cancellationToken);

        var networkSpec = new NetworkSpec(
            Name: config.NetworkName,
            Kind: NetworkKind.LinuxBridge);
        var networkHandle = await networks.AttachAsync(networkSpec, cancellationToken);

        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(spec, id, volumeHandle.Reference, networkHandle.Reference);
        var xmlPath = $"/tmp/plexor-{id}.xml";

        try
        {
            // Write the domain XML to disk, define it, then start it.
            // Two-step so the agent can re-define without starting on
            // create-time errors.
            await File.WriteAllTextAsync(xmlPath, xml, cancellationToken);
            await LibvirtRunner.RunAsync(LibvirtUri, $"define {xmlPath}", cancellationToken);
            await LibvirtRunner.RunAsync(LibvirtUri, $"start {spec.Name}", cancellationToken);
        }
        catch
        {
            // Best-effort cleanup: tear down the volume + network
            // we just allocated. We don't try to undefine the
            // (possibly partially-defined) domain — virsh undefine
            // on a domain that's already gone is harmless and
            // skipping it avoids a second race.
            try
            {
                await volumes.DeleteAsync(volumeHandle, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(
                    cleanupEx,
                    "Best-effort cleanup of volume {Volume} after failed create failed",
                    volumeHandle.Reference);
            }

            try
            {
                await networks.DetachAsync(networkHandle, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(
                    cleanupEx,
                    "Best-effort cleanup of network {Network} after failed create failed",
                    networkHandle.Reference);
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

        var now = clock.GetUtcNow();
        workloads.Register(id, spec.Name, Kind, volumeHandle, networkHandle);
        return new LocalWorkload(
            id,
            spec.Name,
            Kind,
            WorkloadState.Running,
            now,
            now);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StartAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        await LibvirtRunner.RunAsync(LibvirtUri, $"start {entry.DomainName}", cancellationToken);
        workloads.SetState(id, WorkloadState.Running);
        return Snapshot(id, clock.GetUtcNow());
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StopAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        // virsh shutdown is graceful; virsh destroy is forced.
        // v0.1 doesn't escalate; v0.2+ uses libvirt's domain
        // events to detect the transition to "shut off".
        await LibvirtRunner.RunAsync(LibvirtUri, $"shutdown {entry.DomainName}", cancellationToken);
        workloads.SetState(id, WorkloadState.Stopped);
        return Snapshot(id, null);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        // undefines the domain (frees its config but does NOT
        // destroy the underlying disk image). The agent's caller
        // is responsible for any disk cleanup.
        await LibvirtRunner.RunAsync(LibvirtUri, $"undefine {entry.DomainName}", cancellationToken);

        // Free the volume + network we allocated at create-time.
        // Both backends are idempotent — missing handle is a
        // successful no-op (matches the "delete is eventual"
        // semantics the control plane expects).
        await volumes.DeleteAsync(entry.VolumeHandle, cancellationToken);
        await networks.DetachAsync(entry.NetworkHandle, cancellationToken);

        if (!workloads.Remove(id))
        {
            throw new InvalidOperationException(
                $"LibvirtKvmProvider: race removing workload {id}.");
        }

        return new LocalWorkload(
            id,
            entry.DomainName,
            entry.Kind,
            WorkloadState.Stopped,
            clock.GetUtcNow(),
            null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LocalWorkload>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<LocalWorkload>>(
            workloads.Snapshot());
    }

    /// <summary>
    ///     Build a <see cref="LocalWorkload" /> snapshot for
    ///     the given id with the given startedAt timestamp. Helper
    ///     used by start/stop/delete to return a value to the
    ///     agent. Stays on the provider (not file-static) because
    ///     it reads from the <see cref="WorkloadIdMap" /> instance
    ///     state.
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
            clock.GetUtcNow(),
            startedAt);
    }
}