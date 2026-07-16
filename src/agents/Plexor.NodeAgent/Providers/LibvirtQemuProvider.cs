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
// v0.1: domain XML is identical to KVM except for the
// <domain type="qemu"> and machine type. The same qcow2 disk
// + bridge network works. Real v0.2+ might add a per-WorkloadKind
// image-cache (e.g. KVM uses a host-side qcow2 cache;
// TCG uses a read-only base + per-instance qcow2 overlay).
// ============================================================================

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
///     <see cref="IWorkloadProvider" /> for QEMU VMs without KVM
///     acceleration. Same wire format as <see cref="LibvirtKvmProvider" />
///     (same xmlwriter template + same virsh CLI) but a different
///     <see cref="WorkloadKind" /> so the agent's dispatcher routes
///     the right commands to the right backend.
/// </summary>
/// <param name="logger"></param>
/// <remarks>
///     Build a provider that talks to the local
///     libvirt system instance via software-emulated QEMU.
/// </remarks>
public sealed class LibvirtQemuProvider(ILogger<LibvirtQemuProvider> logger) : IWorkloadProvider
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
        var xml = BuildDomainXml(spec, id);
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
            // Tier 3.5 — QEMU provider will adopt IVolumeBackend +
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
        await LibvirtRunner.RunAsync(LibvirtUri, $"undefine {entry.DomainName}", cancellationToken);

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

    /// <summary>
    ///     Build a QEMU domain XML. Same shape as the KVM
    ///     provider's output except:
    ///     - <c>type="qemu"</c> (not "kvm") — libvirt skips the
    ///     KVM acceleration path.
    ///     - <c>machine="pc"</c> — generic PC, not the KVM-optimized
    ///     <c>pc-i440fx</c>. Compatible with the broadest set of
    ///     guests; v0.2+ takes this from spec.Config.
    /// </summary>
    /// <param name="spec"></param>
    /// <param name="id"></param>
    private static string BuildDomainXml(WorkloadSpec spec, Guid id)
    {
        var config = TryDeserializeConfig(spec.Config, out var c)
                ? c
                : new LibvirtQemuConfig();

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
            writer.WriteAttributeString("type", "qemu");
            writer.WriteElementString("name", spec.Name);
            writer.WriteElementString("uuid", id.ToString());
            writer.WriteElementString("memory", Convert.ToString(ramKiB, CultureInfo.InvariantCulture));
            writer.WriteElementString("vcpu", Convert.ToString(vcpu, CultureInfo.InvariantCulture));
            writer.WriteStartElement("os");
            writer.WriteElementString("type", "hvm");
            writer.WriteElementString("machine", config.Machine);
            writer.WriteEndElement(); // os

            writer.WriteStartElement("devices");
            writer.WriteStartElement("disk");
            writer.WriteAttributeString("type", "file");
            writer.WriteAttributeString("device", "disk");
            writer.WriteStartElement("driver");
            writer.WriteAttributeString("name", "qemu");
            writer.WriteAttributeString("type", "qcow2");
            writer.WriteEndElement(); // driver
            writer.WriteStartElement("source");
            writer.WriteAttributeString("file", $"/var/lib/libvirt/images/{spec.Name}.qcow2");
            writer.WriteEndElement(); // source
            writer.WriteStartElement("target");
            writer.WriteAttributeString("dev", "vda");
            writer.WriteAttributeString("bus", "virtio");
            writer.WriteEndElement(); // target
            writer.WriteEndElement(); // disk

            writer.WriteStartElement("interface");
            writer.WriteAttributeString("type", "network");
            writer.WriteStartElement("source");
            writer.WriteAttributeString("network", config.NetworkName);
            writer.WriteEndElement(); // source
            writer.WriteEndElement(); // interface

            writer.WriteEndElement(); // devices
            writer.WriteEndElement(); // domain
        }

        return sb.ToString();
    }

    private static bool TryDeserializeConfig(JsonElement config, out LibvirtQemuConfig result)
    {
        try
        {
            result = config.Deserialize<LibvirtQemuConfig>()
                     ?? new LibvirtQemuConfig();

            return true;
        }
        catch
        {
            result = new LibvirtQemuConfig();
            return false;
        }
    }

    /// <summary>
    ///     Provider-specific config schema (consumed from
    ///     <see cref="WorkloadSpec.Config" />).
    /// </summary>
    /// <param name="RamBytes"></param>
    /// <param name="CpuCores"></param>
    /// <param name="Machine">
    ///     QEMU machine type. <c>pc</c> is the
    ///     generic PC, broadly compatible. v0.1 default. Other
    ///     options: <c>q35</c> (modern ICH9 chipset), <c>isapc</c>
    ///     (legacy), arch-specific (<c>virt</c> on aarch64).
    /// </param>
    /// <param name="NetworkName"></param>
    private sealed record LibvirtQemuConfig(
        long RamBytes = 1L * 1024 * 1024 * 1024,
        int CpuCores = 2,
        string Machine = "pc",
        string NetworkName = "default");
}
