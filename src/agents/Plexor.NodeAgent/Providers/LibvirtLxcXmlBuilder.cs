// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtLxcXmlBuilder — file-static pure-function XML builder for
// the LXC provider. Lives in its own file per
// class-decomposition.md (helpers without DI → file-static class,
// not private method on the backend). Tier 3.5: extracted
// from LibvirtLxcProvider when that file crossed 300 lines.
// ==========================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Providers;

internal static class LibvirtLxcXmlBuilder
{
    /// <summary>
    ///     Build the libvirt domain XML for an LXC system
    ///     container. LXC's XML is fundamentally different from KVM:
    ///     no <c>type='hvm'</c>, no <c>disk</c>, the <c>os</c> has
    ///     <c>init</c> instead of a <c>boot dev</c>.
    /// </summary>
    /// <param name="spec">Operator-supplied spec (config carries RAM / vCPU / init path).</param>
    /// <param name="id">Agent-assigned local id for the new container.</param>
    /// <param name="rootfs">Host path to bind-mount as the container's rootfs. Comes from <c>VolumeHandle.Reference</c>.</param>
    /// <param name="networkBridge">Bridge name (e.g. <c>br-prod-vpc</c>) to attach the container's veth to. Comes from <c>NetworkInterfaceHandle.Reference</c>.</param>
    public static string BuildContainerXml(WorkloadSpec spec, Guid id, string rootfs, string networkBridge)
    {
        var config = TryDeserializeConfig(spec.Config, out var c)
                ? c
                : new LibvirtLxcConfig();

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
            writer.WriteAttributeString("type", "lxc");
            writer.WriteElementString("name", spec.Name);
            writer.WriteElementString("uuid", id.ToString());
            writer.WriteElementString("memory", Convert.ToString(ramKiB, CultureInfo.InvariantCulture));
            writer.WriteElementString("vcpu", Convert.ToString(vcpu, CultureInfo.InvariantCulture));

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

            // Network: bridge supplied by the network backend. The
            // backend (LinuxBridgeBackend by default) returns a
            // bridge name like "br-prod-vpc"; libvirt attaches
            // the container's veth to that bridge.
            writer.WriteStartElement("interface");
            writer.WriteAttributeString("type", "bridge");
            writer.WriteStartElement("source");
            writer.WriteAttributeString("bridge", networkBridge);
            writer.WriteEndElement(); // source
            writer.WriteEndElement(); // interface

            writer.WriteEndElement(); // devices
            writer.WriteEndElement(); // domain
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Resolve the operator-supplied <c>template</c> key from
    ///     <see cref="WorkloadSpec.Config" />. Returns the image ref
    ///     if it's a JSON object with a string <c>template</c>
    ///     property; otherwise <c>null</c> (blank rootfs). The
    ///     <c>IImageRegistry</c> chain (<c>LocalDirImageRegistry</c>
    ///     or <c>HttpImageRegistry</c>) resolves the ref to a real
    ///     path on the node.
    /// </summary>
    /// <param name="spec"></param>
    public static string? ResolveBaseImageRef(WorkloadSpec spec)
    {
        if (spec.Config.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!spec.Config.TryGetProperty("template", out var template))
        {
            return null;
        }

        return template.ValueKind == JsonValueKind.String ? template.GetString() : null;
    }

    /// <summary>
    ///     Parse the provider-specific JSON config, falling back to
    ///     defaults on a missing / malformed payload so the agent
    ///     stays functional even with empty <see cref="WorkloadSpec.Config" />.
    /// </summary>
    /// <param name="config">Raw JSON from the control plane.</param>
    /// <param name="result">Resolved config (defaults if parse failed).</param>
    public static bool TryDeserializeConfig(JsonElement config, out LibvirtLxcConfig result)
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
}

/// <summary>
///     Provider-specific config schema (consumed from
///     <see cref="WorkloadSpec.Config" />). v0.1: defaults if the
///     control plane doesn't supply a value, so the agent stays
///     functional even with empty Config.
/// </summary>
/// <param name="RamBytes">RAM allocation in bytes.</param>
/// <param name="CpuCores">Number of vCPUs.</param>
/// <param name="Init">
///     Path to the init binary inside the
///     container. Defaults to <c>/sbin/init</c> for systemd-based
///     images; set to <c>/sbin/runit</c> or <c>/bin/sh</c> for
///     lighter bases (Alpine, etc.).
/// </param>
public sealed record LibvirtLxcConfig(
    long RamBytes = 1L * 1024 * 1024 * 1024,
    int CpuCores = 2,
    string Init = "/sbin/init");
