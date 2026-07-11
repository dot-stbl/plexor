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

using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers;

/// <summary>
/// <see cref="IWorkloadProvider"/> for LXC system containers via
/// libvirt. Different <see cref="WorkloadKind"/> from KVM (the
/// agent's dispatcher routes by Kind), so the agent runs the
/// same commands against fundamentally different technology.
/// </summary>
public sealed class LibvirtLxcProvider : IWorkloadProvider
{
    /// <summary>The libvirt URI for the local LXC driver. v0.1
    /// hardcodes this; v0.2+ reads it from configuration.</summary>
    public static readonly Uri LibvirtUri = new("lxc:///system");

    /// <summary>Default rootfs base directory. Each container's
    /// rootfs is a subdirectory under here. v0.1: every container
    /// starts from an empty directory; v0.2+ supports cloning a
    /// base image (Ubuntu 24.04 cloud image, Alpine 3.21, etc.) via
    /// <c>spec.Config.template</c>.</summary>
    public const string DefaultRootfsBase = "/var/lib/plx-lxc";

    private readonly ILogger<LibvirtLxcProvider> logger;
    private readonly WorkloadIdMap workloads = new();

    /// <summary>Build a provider that talks to the local libvirt
    /// LXC driver.</summary>
    public LibvirtLxcProvider(ILogger<LibvirtLxcProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.Lxc();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(WorkloadSpec spec, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var rootfs = Path.Combine(DefaultRootfsBase, spec.Name);
        var xml = BuildContainerXml(spec, id, rootfs);
        var xmlPath = $"/tmp/plexor-{id}.xml";

        try
        {
            // v0.1: empty rootfs. mkdir -p the directory; v0.2+
            // clones a template image if spec.Config has
            // 'template' = '<image-name>'.
            Directory.CreateDirectory(rootfs);

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

        workloads.Register(id, spec.Name, Kind);
        return new LocalWorkload(
            Id: id,
            Name: spec.Name,
            Kind: Kind,
            State: WorkloadState.Running,
            CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StartAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        await LibvirtRunner.RunAsync(LibvirtUri, $"start {entry.DomainName}", cancellationToken);
        workloads.SetState(id, WorkloadState.Running);
        return Snapshot(id, startedAt: DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StopAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        await LibvirtRunner.RunAsync(LibvirtUri, $"shutdown {entry.DomainName}", cancellationToken);
        workloads.SetState(id, WorkloadState.Stopped);
        return Snapshot(id, startedAt: null);
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = workloads.GetOrThrow(id);
        // LXC undefines the domain AND we drop the rootfs directory.
        // The rootfs can be large; v0.1 just rm -rf's it.
        await LibvirtRunner.RunAsync(LibvirtUri, $"undefine {entry.DomainName}", cancellationToken);

        var rootfs = Path.Combine(DefaultRootfsBase, entry.DomainName);
        try
        {
            if (Directory.Exists(rootfs))
            {
                Directory.Delete(rootfs, recursive: true);
            }
        }
        catch (Exception cleanup)
        {
            logger.LogWarning(
                cleanup,
                "Failed to remove rootfs {Path}; leaving for manual cleanup",
                rootfs);
        }

        if (!workloads.Remove(id))
        {
            throw new InvalidOperationException(
                $"LibvirtLxcProvider: race removing workload {id}.");
        }

        return new LocalWorkload(
            Id: id,
            Name: entry.DomainName,
            Kind: entry.Kind,
            State: WorkloadState.Stopped,
            CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LocalWorkload>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<LocalWorkload>>(
            workloads.Snapshot(Environment.MachineName));
    }

    /// <summary>Build a <see cref="LocalWorkload"/> snapshot for
    /// the given id with the given startedAt timestamp.</summary>
    private LocalWorkload Snapshot(Guid id, DateTimeOffset? startedAt)
    {
        var entry = workloads.GetOrThrow(id);
        return new LocalWorkload(
            Id: id,
            Name: entry.DomainName,
            Kind: entry.Kind,
            State: entry.State,
            CreatedAt: DateTimeOffset.UtcNow,
            StartedAt: startedAt);
    }

    /// <summary>Build the libvirt domain XML for an LXC system
    /// container. LXC's XML is fundamentally different from KVM:
    /// no <c>type='hvm'</c>, no <c>disk</c>, the <c>os</c> has
    /// <c>init</c> instead of a <c>boot dev</c>.</summary>
    private static string BuildContainerXml(WorkloadSpec spec, Guid id, string rootfs)
    {
        var config = TryDeserializeConfig(spec.Config, out var c)
            ? c
            : new LibvirtLxcConfig();

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
            writer.WriteAttributeString("type", "lxc");
            writer.WriteElementString("name", spec.Name);
            writer.WriteElementString("uuid", id.ToString());
            writer.WriteElementString("memory", Convert.ToString(ramKiB, System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteElementString("vcpu", Convert.ToString(vcpu, System.Globalization.CultureInfo.InvariantCulture));

            writer.WriteStartElement("os");
            writer.WriteElementString("type", "exe");
            writer.WriteElementString("init", config.Init);
            writer.WriteEndElement(); // os

            writer.WriteStartElement("devices");

            // Filesystem bind-mount: the container's / is the
            // host's rootfs directory. Real impl would use a
            // disk-backed rootfs (qcow2, raw) for production.
            writer.WriteStartElement("filesystem");
            writer.WriteAttributeString("type", "mount");
            writer.WriteStartElement("source");
            writer.WriteAttributeString("dir", rootfs);
            writer.WriteEndElement(); // source
            writer.WriteStartElement("target");
            writer.WriteAttributeString("dir", "/");
            writer.WriteEndElement(); // target
            writer.WriteEndElement(); // filesystem

            // Console so the agent's user (or libvirt's tools)
            // can attach; LXC needs a pty for the init.
            writer.WriteStartElement("console");
            writer.WriteAttributeString("type", "pty");
            writer.WriteEndElement(); // console

            // Network: bridge onto the default libvirt network.
            writer.WriteStartElement("interface");
            writer.WriteAttributeString("type", "bridge");
            writer.WriteStartElement("source");
            writer.WriteAttributeString("bridge", config.BridgeName);
            writer.WriteEndElement(); // source
            writer.WriteEndElement(); // interface

            writer.WriteEndElement(); // devices
            writer.WriteEndElement(); // domain
        }

        return sb.ToString();
    }

    private static bool TryDeserializeConfig(JsonElement config, out LibvirtLxcConfig result)
    {
        try
        {
            result = config.Deserialize<LibvirtLxcConfig>()
                ?? new LibvirtLxcConfig();
            return true;
        }
        catch
        {
            result = new LibvirtLxcConfig();
            return false;
        }
    }

    /// <summary>Provider-specific config schema (consumed from
    /// <see cref="WorkloadSpec.Config"/>). v0.1: defaults if the
    /// control plane doesn't supply a value, so the agent stays
    /// functional even with empty Config.</summary>
    /// <param name="Init">Path to the init binary inside the
    /// container. Defaults to <c>/sbin/init</c> for systemd-based
    /// images; set to <c>/sbin/runit</c> or <c>/bin/sh</c> for
    /// lighter bases (Alpine, etc.).</param>
    /// <param name="BridgeName">Libvirt network bridge to attach
    /// the container's veth to. Defaults to <c>virbr0</c> (the
    /// libvirt default NAT bridge).</param>
    private sealed record LibvirtLxcConfig(
        long RamBytes = 1L * 1024 * 1024 * 1024,
        int CpuCores = 2,
        string Init = "/sbin/init",
        string BridgeName = "virbr0");
}