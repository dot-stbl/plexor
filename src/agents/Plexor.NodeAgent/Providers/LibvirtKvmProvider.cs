// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtKvmProvider — IWorkloadProvider for KVM virtual machines
// via libvirt. v0.1 shells out to the `virsh` CLI (via
// LibvirtRunner); the future v0.2+ impl swaps in LibvirtClient
// for richer async + no shell-quoting footguns.
//
// Compute stack wiring:
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
// ==========================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.Compute;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers;

/// <summary>
///     <see cref="IWorkloadProvider" /> for KVM VMs via libvirt. v0.1
///     uses the <c>virsh</c> CLI; future v0.2+ uses LibvirtClient.
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
public sealed class LibvirtKvmProvider(
    IVolumeBackend volumes,
    INetworkBackend networks,
    ILogger<LibvirtKvmProvider> logger) : IWorkloadProvider
{
    /// <summary>
    ///     The libvirt URI for KVM/QEMU on the local
    ///     system. v0.1 hardcodes this; v0.2+ reads it from
    ///     configuration so the agent can target remote libvirt
    ///     hosts.
    /// </summary>
    public static readonly Uri LibvirtUri = new("qemu:///system");

    private readonly WorkloadIdMap workloads = new();

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.Vm();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(WorkloadSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var config = TryDeserializeConfig(spec.Config, out var c)
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

        var xml = BuildDomainXml(spec, id, volumeHandle.Reference, networkHandle.Reference);
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
    ///     the given id with the given startedAt timestamp. Helper
    ///     used by start/stop/delete to return a value to the agent.
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

    /// <summary>
    ///     Build a minimal libvirt domain XML for the given
    ///     spec. v0.1: one disk, one network interface, no balloon
    ///     device. Real impl reads additional config from
    ///     <see cref="WorkloadSpec.Config" /> (opaque JSON the
    ///     provider owns).
    /// </summary>
    /// <param name="spec"></param>
    /// <param name="id"></param>
    /// <param name="volumePath">
    ///     Absolute path on the host filesystem — comes from
    ///     <see cref="VolumeHandle.Reference" /> for LocalDirStorage.
    /// </param>
    /// <param name="networkBridge">
    ///     Bridge name — comes from
    ///     <see cref="NetworkInterfaceHandle.Reference" /> for
    ///     LinuxBridgeBackend.
    /// </param>
    private static string BuildDomainXml(
        WorkloadSpec spec,
        Guid id,
        string volumePath,
        string networkBridge)
    {
        var config = TryDeserializeConfig(spec.Config, out var c)
                ? c
                : new LibvirtKvmConfig();

        // v0.1: defaults if Config is missing fields. Future:
        // the control plane passes these explicitly.
        var ramKiB = config.RamBytes / 1024;
        var vcpu = config.CpuCores;

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = true
        };

        var sb = new StringBuilder();

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartElement("domain");
            writer.WriteAttributeString("type", "kvm");
            writer.WriteElementString("name", spec.Name);
            writer.WriteElementString("uuid", id.ToString());
            writer.WriteElementString("memory", Convert.ToString(ramKiB, CultureInfo.InvariantCulture));
            writer.WriteElementString("vcpu", Convert.ToString(vcpu, CultureInfo.InvariantCulture));

            writer.WriteStartElement("os");
            writer.WriteElementString("type", "hvm");
            writer.WriteElementString("boot", "dev", "hd");
            writer.WriteEndElement(); // os

            writer.WriteStartElement("features");
            writer.WriteElementString("acpi", "");
            writer.WriteElementString("apic", "");
            writer.WriteEndElement(); // features

            writer.WriteStartElement("clock");
            writer.WriteAttributeString("offset", "utc");
            writer.WriteEndElement(); // clock

            writer.WriteStartElement("devices");
            writer.WriteStartElement("emulator");
            writer.WriteString("/dev/kvm");
            writer.WriteEndElement(); // emulator

            writer.WriteStartElement("disk");
            writer.WriteAttributeString("type", "file");
            writer.WriteAttributeString("device", "disk");
            writer.WriteStartElement("driver");
            writer.WriteAttributeString("name", "qemu");
            writer.WriteAttributeString("type", "qcow2");
            writer.WriteEndElement(); // driver
            writer.WriteStartElement("source");
            writer.WriteAttributeString("file", volumePath);
            writer.WriteEndElement(); // source
            writer.WriteStartElement("target");
            writer.WriteAttributeString("dev", "vda");
            writer.WriteAttributeString("bus", "virtio");
            writer.WriteEndElement(); // target
            writer.WriteEndElement(); // disk

            writer.WriteStartElement("interface");
            writer.WriteAttributeString("type", "bridge");
            writer.WriteStartElement("source");
            writer.WriteAttributeString("bridge", networkBridge);
            writer.WriteEndElement(); // source
            writer.WriteEndElement(); // interface

            writer.WriteStartElement("serial");
            writer.WriteAttributeString("type", "pty");
            writer.WriteStartElement("target");
            writer.WriteAttributeString("type", "isa-serial");
            writer.WriteAttributeString("port", "0");
            writer.WriteEndElement(); // target
            writer.WriteEndElement(); // serial

            writer.WriteStartElement("console");
            writer.WriteAttributeString("type", "pty");
            writer.WriteStartElement("target");
            writer.WriteAttributeString("type", "serial");
            writer.WriteAttributeString("port", "0");
            writer.WriteEndElement(); // target
            writer.WriteEndElement(); // console

            writer.WriteEndElement(); // devices
            writer.WriteEndElement(); // domain
        }

        return sb.ToString();
    }

    private static bool TryDeserializeConfig(JsonElement config, out LibvirtKvmConfig result)
    {
        try
        {
            result = config.Deserialize<LibvirtKvmConfig>()
                     ?? new LibvirtKvmConfig();

            return true;
        }
        catch
        {
            result = new LibvirtKvmConfig();
            return false;
        }
    }

    /// <summary>
    ///     Provider-specific config schema (consumed from
    ///     <see cref="WorkloadSpec.Config" />). v0.1: defaults if the
    ///     control plane doesn't supply a value, so the agent stays
    ///     functional even with empty Config.
    /// </summary>
    /// <param name="RamBytes">RAM allocation in bytes.</param>
    /// <param name="CpuCores">Number of vCPUs.</param>
    /// <param name="NetworkName">Logical network name (matches libvirt network name).</param>
    /// <param name="BaseImageRef">Operator-facing image ref resolved via <see cref="IImageRegistry" />.</param>
    private sealed record LibvirtKvmConfig(
        long RamBytes = 1L * 1024 * 1024 * 1024,
        int CpuCores = 2,
        string NetworkName = "default",
        string? BaseImageRef = null);
}