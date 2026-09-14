// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtKvmXmlBuilder — file-static pure-function XML builder for
// the KVM provider. Lives in its own file per
// class-decomposition.md (helpers without DI → file-static class,
// not private method on the backend).
//
// Extracted in Sprint 3 (item 6) — the original LibvirtKvmProvider
// had BuildDomainXml + TryDeserializeConfig + LibvirtKvmConfig as
// private members; KVM was the last provider to follow the
// builder-file pattern that QEMU + LXC already use.
// ==========================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Providers;

internal static class LibvirtKvmXmlBuilder
{
    /// <summary>
    ///     Build a KVM domain XML. v0.1: one disk, one network
    ///     interface, no balloon device. Real impl reads additional
    ///     config from <see cref="WorkloadSpec.Config" /> (opaque
    ///     JSON the provider owns).
    /// </summary>
    /// <param name="spec">Operator-supplied spec (config carries RAM / vCPU / network name / base image ref).</param>
    /// <param name="id">Agent-assigned local id for the new VM.</param>
    /// <param name="volumePath">Disk image path on the host filesystem. Comes from <c>VolumeHandle.Reference</c>.</param>
    /// <param name="networkBridge">Bridge name to attach the VM's NIC to. Comes from <c>NetworkInterfaceHandle.Reference</c>.</param>
    public static string BuildDomainXml(
        WorkloadSpec spec,
        Guid id,
        string volumePath,
        string networkBridge)
    {
        var config = LibvirtConfigDeserializer.TryDeserialize(spec.Config, () => new LibvirtKvmConfig(), out var c)
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
/// <param name="BaseImageRef">Operator-facing image ref resolved via <c>IImageRegistry</c>.</param>
public sealed record LibvirtKvmConfig(
    long RamBytes = 1L * 1024 * 1024 * 1024,
    int CpuCores = 2,
    string NetworkName = "default",
    string? BaseImageRef = null)
{
    /// <summary>
    ///     Public parameterless constructor — required by
    ///     <see cref="LibvirtConfigDeserializer.TryDeserialize{T}" />
    ///     which falls back to <c>new T()</c> on a null or
    ///     unparseable payload. The compiler synthesises one
    ///     for records with all-default primary-ctor params, but
    ///     only as a private/internal member; we re-declare it
    ///     public so the generic constraint is satisfied.
    /// </summary>
    public LibvirtKvmConfig()
        : this(RamBytes: 1L * 1024 * 1024 * 1024,
               CpuCores: 2,
               NetworkName: "default",
               BaseImageRef: null)
    {
    }
}