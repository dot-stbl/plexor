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
using System.Xml;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent.Providers;

internal static class LibvirtKvmXmlBuilder
{
    /// <summary>
    ///     Build a KVM domain XML. v0.1: one disk, one network
    ///     interface, optional cloud-init cidata CD-ROM (when
    ///     <paramref name="cidataIsoPath" /> is non-null), no
    ///     balloon device. Real impl reads additional config
    ///     from <see cref="WorkloadSpec.Config" /> (opaque JSON
    ///     the provider owns).
    /// </summary>
    /// <param name="spec">Operator-supplied spec (config carries RAM / vCPU / network name / base image ref).</param>
    /// <param name="id">Agent-assigned local id for the new VM.</param>
    /// <param name="volumePath">Disk image path on the host filesystem. Comes from <c>VolumeHandle.Reference</c>.</param>
    /// <param name="networkBridge">Bridge name to attach the VM's NIC to. Comes from <c>NetworkInterfaceHandle.Reference</c>.</param>
    /// <param name="cidataIsoPath">
    ///     Absolute path to a pre-built cloud-init cidata ISO
    ///     (see <c>CidataBuilder</c>). When non-null, attached
    ///     as a CD-ROM device; when null, the domain has no
    ///     cidata device. v0.1: a non-null value implies
    ///     <c>Bus=Ide, Device=vdb</c> so cloud-init's NoCloud
    ///     seed picks the right disk regardless of the boot disk
    ///     order.
    /// </param>
    public static string BuildDomainXml(
        WorkloadSpec spec,
        Guid id,
        string volumePath,
        string networkBridge,
        string? cidataIsoPath)
    {
        var config = LibvirtConfigDeserializer.TryDeserialize(spec.Config, static () => new LibvirtKvmConfig(), out var c)
                ? c
                : new LibvirtKvmConfig();

        // v0.1: defaults if Config is missing fields. Future:
        // the control plane passes these explicitly.
        var ramKiB = config.RamBytes / 1024;
        var vcpu = config.Vcpu;

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

            // Optional cloud-init cidata CD-ROM. Wired by the
            // provider when the spec carries a public SSH key
            // (see CidataBuilder in #9). We attach as a read-only
            // IDE device on vdb — cloud-init's NoCloud datasource
            // walks every disk and picks the one labelled
            // "cidata"; we don't need to mark it explicitly.
            if (cidataIsoPath is not null)
            {
                writer.WriteStartElement("disk");
                writer.WriteAttributeString("type", "file");
                writer.WriteAttributeString("device", "cdrom");
                writer.WriteStartElement("driver");
                writer.WriteAttributeString("name", "qemu");
                writer.WriteAttributeString("type", "raw");
                writer.WriteEndElement(); // driver
                writer.WriteStartElement("source");
                writer.WriteAttributeString("file", cidataIsoPath);
                writer.WriteEndElement(); // source
                writer.WriteStartElement("target");
                writer.WriteAttributeString("dev", "vdb");
                writer.WriteAttributeString("bus", "ide");
                writer.WriteEndElement(); // target
                writer.WriteStartElement("readonly");
                writer.WriteEndElement(); // readonly
                writer.WriteEndElement(); // disk (cdrom)
            }

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
///
///     <para>
///     Wire-format JSON keys (PascalCase): <c>Vcpu</c>,
///     <c>RamBytes</c>, <c>DiskBytes</c>, <c>NetworkName</c>,
///     <c>BaseImageRef</c>, <c>SshPublicKey</c>,
///     <c>SshKeyFingerprint</c>. Unknown keys are ignored by the
///     deserialiser (System.Text.Json default).
///     </para>
/// </summary>
/// <param name="RamBytes">RAM allocation in bytes.</param>
/// <param name="Vcpu">Logical vCPU count.</param>
/// <param name="DiskBytes">
///     Root disk size in bytes. When <c>null</c> the provider
///     derives the volume size from RAM (RAM * 4) so a missing
///     field is still functional. The control plane passes an
///     explicit value from the operator's <c>--disk-gb</c> flag.
/// </param>
/// <param name="NetworkName">Logical network name (matches libvirt network name).</param>
/// <param name="BaseImageRef">Operator-facing image ref resolved via <c>IImageRegistry</c>.</param>
/// <param name="SshPublicKey">
///     OpenSSH public key content (one line, e.g.
///     <c>"ssh-ed25519 AAAA… user@host"</c>). When non-null, the
///     provider attaches a cloud-init cidata ISO that injects
///     this key as the <c>root</c> user's authorized key
///     (see <c>CidataBuilder</c>, added in P1 #9). Mutually
///     compatible with <paramref name="SshKeyFingerprint" />:
///     the fingerprint is logged for audit; the public key
///     content is what cloud-init needs.
/// </param>
/// <param name="SshKeyFingerprint">
///     Stable fingerprint of <paramref name="SshPublicKey" />.
///     Logged for audit / operator traceability; the actual key
///     content is what gets injected into cloud-init. When
///     <paramref name="SshPublicKey" /> is null, the fingerprint
///     is recorded but no key is injected.
/// </param>
public sealed record LibvirtKvmConfig(
    long RamBytes = 1L * 1024 * 1024 * 1024,
    int Vcpu = 2,
    long? DiskBytes = null,
    string NetworkName = "default",
    string? BaseImageRef = null,
    string? SshPublicKey = null,
    string? SshKeyFingerprint = null)
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
               Vcpu: 2,
               DiskBytes: null,
               NetworkName: "default",
               BaseImageRef: null,
               SshPublicKey: null,
               SshKeyFingerprint: null)
    {
    }
}
