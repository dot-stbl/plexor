// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtKvmProvider — IWorkloadProvider impl for KVM virtual
// machines via libvirt. v0.1 shells out to the `virsh` CLI; the
// future v0.2+ impl swaps in the libvirt C# binding (LibvirtClient)
// for richer state, no shell-quoting footguns, and proper async.
//
// Each Plexor workload maps to a single libvirt domain whose name
// is the WorkloadSpec.Name. The provider's local id is generated
// at create-time and tracked in a ConcurrentDictionary so the
// agent's start/stop/delete calls resolve to the right domain.
//
// Libvirt is hard-fail loud: every CLI error becomes an exception
// the executor catches, and the agent reports Failed back to the
// host. The provider never silently drops a command.
// ============================================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers;

/// <summary>
/// <see cref="IWorkloadProvider"/> for KVM VMs via libvirt. v0.1
/// uses the <c>virsh</c> CLI; future v0.2+ uses LibvirtClient.
/// </summary>
public sealed class LibvirtKvmProvider : IWorkloadProvider
{
    private readonly ILogger<LibvirtKvmProvider> logger;
    private readonly ConcurrentDictionary<Guid, LocalWorkload> workloads = new();

    /// <summary>The libvirt URI this provider connects to. v0.1
    /// hardcodes the local system URI; v0.2+ reads it from
    /// configuration so the agent can target remote libvirt
    /// hosts (e.g. the shared libvirt on a single-host cluster).</summary>
    public const string LibvirtUri = "qemu:///system";

    /// <summary>Build a provider that talks to the local
    /// libvirt system instance.</summary>
    public LibvirtKvmProvider(ILogger<LibvirtKvmProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.Vm();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(WorkloadSpec spec, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var xml = BuildDomainXml(spec, id);
        var xmlPath = $"/tmp/plexor-{id}.xml";

        try
        {
            // Write the domain XML to disk, define it, then start it.
            // Two-step so the agent can re-define without starting on
            // create-time errors.
            await File.WriteAllTextAsync(xmlPath, xml, ct);
            await RunVirshAsync($"define {xmlPath}", ct);
            await RunVirshAsync($"start {spec.Name}", ct);
        }
        catch
        {
            // Best-effort cleanup: undefine anything that got
            // partially created so we don't leave orphan domains.
            try { await RunVirshAsync($"undefine {spec.Name}", CancellationToken.None); }
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
            try { File.Delete(xmlPath); }
            catch (Exception cleanup)
            {
                logger.LogDebug(
                    cleanup,
                    "Could not delete temp xml {Path}; leaving in /tmp",
                    xmlPath);
            }
        }

        var workload = new LocalWorkload(
            Id: id,
            Name: spec.Name,
            Kind: Kind,
            State: WorkloadState.Running,
            CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: DateTimeOffset.UtcNow);
        workloads[id] = workload;
        return workload;
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StartAsync(Guid id, CancellationToken ct)
    {
        var workload = GetOrThrow(id);
        await RunVirshAsync($"start {workload.Name}", ct);
        return MarkState(workload, WorkloadState.Running, startedAt: DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StopAsync(Guid id, CancellationToken ct)
    {
        var workload = GetOrThrow(id);
        // virsh shutdown is graceful; virsh destroy is forced.
        // We use shutdown first; if it doesn't transition the
        // domain to "shut off" within a reasonable window, the
        // executor should escalate to destroy. v0.1 doesn't escalate
        // — the host sees a long-running command and can cancel
        // it. Future: use libvirt's domain events to detect the
        // transition.
        await RunVirshAsync($"shutdown {workload.Name}", ct);
        return MarkState(workload, WorkloadState.Stopped, startedAt: null);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> DeleteAsync(Guid id, CancellationToken ct)
    {
        var workload = GetOrThrow(id);
        // undefines the domain (frees its config but does NOT
        // destroy the underlying disk image). The agent's caller
        // is responsible for any disk cleanup.
        await RunVirshAsync($"undefine {workload.Name}", ct);
        if (!workloads.TryRemove(id, out _))
        {
            throw new InvalidOperationException(
                $"LibvirtKvmProvider: race removing workload {id}.");
        }
        return workload with { State = WorkloadState.Stopped };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalWorkload>> ListAsync(CancellationToken ct)
    {
        // v0.1: return our in-memory list. Future: `virsh list
        // --all` for the full libvirt view.
        await Task.CompletedTask;
        return workloads.Values.ToArray();
    }

    /// <summary>Look up a workload by id, throw if missing. The
    /// agent's executor catches this and reports Failed back to
    /// the host.</summary>
    private LocalWorkload GetOrThrow(Guid id)
    {
        if (!workloads.TryGetValue(id, out var workload))
        {
            throw new InvalidOperationException(
                $"LibvirtKvmProvider: no workload with id {id}.");
        }

        return workload;
    }

    /// <summary>Return a copy of <paramref name="workload"/> with
    /// its state and (optionally) <c>StartedAt</c> updated.
    /// Tracking happens in-place for the local id; the
    /// provider keeps the agent's view in sync via the
    /// returned <see cref="LocalWorkload"/>.</summary>
    private LocalWorkload MarkState(
        LocalWorkload workload, WorkloadState state, DateTimeOffset? startedAt)
    {
        var updated = workload with { State = state, StartedAt = startedAt };
        workloads[workload.Id] = updated;
        return updated;
    }

    /// <summary>Run <c>virsh &lt;args&gt;</c> with the configured
    /// URI, fail on non-zero exit, return trimmed stdout.</summary>
    private static async Task<string> RunVirshAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "virsh",
            ArgumentList = { "-c", LibvirtUri },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException(
                "LibvirtKvmProvider: failed to start virsh (process.Start returned null).");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"virsh {args} failed (exit {process.ExitCode}): {stderr}".Trim());
        }

        return stdout.ToString().TrimEnd();
    }

    /// <summary>Build a minimal libvirt domain XML for the given
    /// spec. v0.1: one disk, one network interface, no balloon
    /// device. Real impl reads additional config from
    /// <see cref="WorkloadSpec.Config"/> (opaque JSON the
    /// provider owns).</summary>
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
            OmitXmlDeclaration = true,
        };
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartElement("domain");
            writer.WriteAttributeString("type", "kvm");
            writer.WriteElementString("name", spec.Name);
            writer.WriteElementString("uuid", id.ToString());
            writer.WriteElementString("memory", Convert.ToString(ramKiB, System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteElementString("vcpu", Convert.ToString(vcpu, System.Globalization.CultureInfo.InvariantCulture));

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

    /// <summary>Provider-specific config schema (consumed from
    /// <see cref="WorkloadSpec.Config"/>). v0.1: defaults if the
    /// control plane doesn't supply a value, so the agent stays
    /// functional even with empty Config.</summary>
    private sealed record LibvirtKvmConfig(
        long RamBytes = 1L * 1024 * 1024 * 1024,
        int CpuCores = 2,
        string NetworkName = "default");
}