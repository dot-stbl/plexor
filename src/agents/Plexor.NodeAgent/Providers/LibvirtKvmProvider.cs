// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtKvmProvider — IWorkloadProvider impl for KVM virtual
// machines via libvirt. v0.1 shells out to the `virsh` CLI (via
// LibvirtRunner); the future v0.2+ impl swaps in LibvirtClient.
//
// Each Plexor workload maps to a single libvirt domain whose
// name is the WorkloadSpec.Name. The provider's local id is
// generated at create-time and tracked in WorkloadIdMap so
// the agent's start/stop/delete calls resolve to the right
// domain.
//
// v0.1: minimal domain XML — one qcow2 disk, one bridge
// network, no balloon device. v0.2+ parses the rest of
// WorkloadSpec.Config.
// ============================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers;

/// <summary>
///     <see cref="IWorkloadProvider" /> for KVM VMs via libvirt. v0.1
///     uses the <c>virsh</c> CLI; future v0.2+ uses LibvirtClient.
/// </summary>
/// <remarks>
///     Build a provider that talks to the local
///     libvirt system instance via KVM.
/// </remarks>
public sealed class LibvirtKvmProvider(ILogger<LibvirtKvmProvider> logger) : IWorkloadProvider
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
        var id = Guid.NewGuid();
        var xml = BuildDomainXml(spec, id);
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
            // Best-effort cleanup: undefine anything that got
            // partially created so we don't leave orphan domains.
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

        workloads.Register(id, spec.Name, Kind);
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
    private static string BuildDomainXml(WorkloadSpec spec, Guid id)
    {
        // The spec is opaque to us; we read what we know and
        // ignore the rest for v0.1. Real impl deserializes
        // spec.Config into a typed record.
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
    private sealed record LibvirtKvmConfig(
        long RamBytes = 1L * 1024 * 1024 * 1024,
        int CpuCores = 2,
        string NetworkName = "default");
}
