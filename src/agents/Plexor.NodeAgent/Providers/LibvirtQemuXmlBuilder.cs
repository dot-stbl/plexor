// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtQemuXmlBuilder — file-static pure-function XML builder for
// the QEMU (no-KVM) provider. Lives in its own file per
// class-decomposition.md (helpers without DI → file-static class,
// not private method on the backend). Tier 3.5: extracted
// from LibvirtQemuProvider when that file crossed 300 lines.
// ==========================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Providers;

internal static class LibvirtQemuXmlBuilder
{
    /// <summary>
    ///     Build a QEMU domain XML. Same shape as the KVM
    ///     provider's output except:
    ///     - <c>type="qemu"</c> (not "kvm") — libvirt skips the
    ///     KVM acceleration path.
    ///     - <c>machine="pc"</c> — generic PC, not the KVM-optimized
    ///     <c>pc-i440fx</c>. Compatible with the broadest set of
    ///     guests; v0.2+ takes this from spec.Config.
    /// </summary>
    /// <param name="spec">Operator-supplied workload spec (config carries RAM / vCPU / machine type).</param>
    /// <param name="id">Agent-assigned local id for the new VM.</param>
    /// <param name="volumePath">Disk image path on the host filesystem. Comes from <c>VolumeHandle.Reference</c>.</param>
    /// <param name="networkBridge">Bridge name to attach the VM's NIC to. Comes from <c>NetworkInterfaceHandle.Reference</c>.</param>
    public static string BuildDomainXml(WorkloadSpec spec, Guid id, string volumePath, string networkBridge)
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
            writer.WriteAttributeString("file", volumePath);
            writer.WriteEndElement(); // source
            writer.WriteStartElement("target");
            writer.WriteAttributeString("dev", "vda");
            writer.WriteAttributeString("bus", "virtio");
            writer.WriteEndElement(); // target
            writer.WriteEndElement(); // disk

            // Tier 3.5: bridge comes from INetworkBackend (typically
            // LinuxBridgeBackend returning a name like "br-prod-vpc").
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
    ///     Parse the provider-specific JSON config, falling back to
    ///     defaults on a missing / malformed payload so the agent
    ///     stays functional even with empty <see cref="WorkloadSpec.Config" />.
    /// </summary>
    /// <param name="config">Raw JSON from the control plane.</param>
    /// <param name="result">Resolved config (defaults if parse failed).</param>
    public static bool TryDeserializeConfig(JsonElement config, out LibvirtQemuConfig result)
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
}

/// <summary>
///     Provider-specific config schema (consumed from
///     <see cref="WorkloadSpec.Config" />). v0.1: defaults if the
///     control plane doesn't supply a value, so the agent stays
///     functional even with empty Config.
/// </summary>
public sealed record LibvirtQemuConfig(
    long RamBytes = 1L * 1024 * 1024 * 1024,
    int CpuCores = 2,
    string Machine = "pc");
