// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtKvmXmlBuilder unit tests — pure-function assertions over the
// generated domain XML. Each test exercises a single shape concern:
// defaults, custom Vcpu/RamBytes/DiskBytes, network bridge, cidata CD-ROM
// attachment, optional fields like SshPublicKey.
//
// The XML builder is the source of truth for "what does a Plexor KVM
// domain look like". Anything that flows into a virsh create call is
// asserted here; the IWorkloadProvider integration test (which actually
// shells out to virsh + qemu-img) is the boot-proof suite in
// tests/integration/Plexor.NodeAgent.Integration (P1 #12).
// ==========================================================================

using System.Text.Json;
using Plexor.NodeAgent.Providers;
using Plexor.Shared.NodeApi;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Providers;

public sealed class LibvirtKvmXmlBuilderShould
{
    [Fact(DisplayName = "Given default config, when BuildDomainXml, then type=kvm, name, uuid, default Vcpu/RamBytes present")]
    public void DefaultsTo2VcpuAnd1GiBRam()
    {
        var id = Guid.NewGuid();
        var spec = NewSpec("vm-default", "{}");
        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(
            spec,
            id,
            volumePath: "/var/lib/plexor/volumes/vm-default.qcow2",
            networkBridge: "br-default",
            cidataIsoPath: null);

        xml.ShouldContain("type=\"kvm\"");
        xml.ShouldContain("<name>vm-default</name>");
        xml.ShouldContain($"<uuid>{id}</uuid>");
        xml.ShouldContain("<memory>1048576</memory>");  // 1 GiB / 1024 = 1048576 KiB
        xml.ShouldContain("<vcpu>2</vcpu>");
    }

    [Fact(DisplayName = "Given explicit Vcpu/RamBytes/DiskBytes, when BuildDomainXml, then XML carries the values")]
    public void CustomVcpuRamBytesReflectedInXml()
    {
        var spec = NewSpec("vm-bigger", /*lang=json,strict*/ """
            {
              "Vcpu": 8,
              "RamBytes": 4294967296,
              "DiskBytes": 53687091200
            }
            """);
        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(
            spec,
            Guid.NewGuid(),
            "/var/lib/plexor/volumes/vm-bigger.qcow2",
            "br-default",
            cidataIsoPath: null);

        xml.ShouldContain("<vcpu>8</vcpu>");
        xml.ShouldContain("<memory>4194304</memory>");  // 4 GiB / 1024
        // DiskBytes is only used by the provider (to size the
        // volume); the domain XML doesn't carry it directly.
    }

    [Fact(DisplayName = "Given networkBridge='br-prod', when BuildDomainXml, then XML references br-prod")]
    public void NetworkBridgeEmittedAsBridgeSource()
    {
        var spec = NewSpec("vm-net", "{}");
        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(
            spec,
            Guid.NewGuid(),
            "/var/lib/plexor/volumes/vm-net.qcow2",
            networkBridge: "br-prod",
            cidataIsoPath: null);

        xml.ShouldContain("type=\"bridge\"");
        xml.ShouldContain("bridge=\"br-prod\"");
    }

    [Fact(DisplayName = "Given cidataIsoPath=null, when BuildDomainXml, then no cdrom device in XML")]
    public void NoCidataDeviceWhenPathNull()
    {
        var spec = NewSpec("vm-no-cidata", "{}");
        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(
            spec,
            Guid.NewGuid(),
            "/var/lib/plexor/volumes/vm-no-cidata.qcow2",
            "br-default",
            cidataIsoPath: null);

        xml.ShouldNotContain("device=\"cdrom\"");
    }

    [Fact(DisplayName = "Given cidataIsoPath, when BuildDomainXml, then IDE cdrom device references the path")]
    public void CidataDeviceAttachedAsIdeCdrom()
    {
        var spec = NewSpec("vm-with-cidata", "{}");
        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(
            spec,
            Guid.NewGuid(),
            "/var/lib/plexor/volumes/vm-with-cidata.qcow2",
            "br-default",
            cidataIsoPath: "/var/lib/plexor/cidata/vm-with-cidata-cidata.iso");

        xml.ShouldContain("device=\"cdrom\"");
        xml.ShouldContain("type=\"raw\"");
        xml.ShouldContain("file=\"/var/lib/plexor/cidata/vm-with-cidata-cidata.iso\"");
        xml.ShouldContain("dev=\"vdb\"");
        xml.ShouldContain("bus=\"ide\"");
        xml.ShouldContain("<readonly");
    }

    [Fact(DisplayName = "Given Config with type-mismatched Vcpu (string instead of int), when BuildDomainXml, then defaults are applied")]
    public void TypeMismatchedConfigFallsBackToDefaults()
    {
        // The deserialiser catches JsonException; the builder
        // uses defaults (RamBytes=1GiB, Vcpu=2). This is the
        // "control plane sent the wrong shape" path — the agent
        // stays functional rather than failing the create on
        // a single bad field.
        var spec = NewSpec("vm-bad", /*lang=json,strict*/ """{"Vcpu":"not-a-number","RamBytes":2147483648}""");

        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(
            spec,
            Guid.NewGuid(),
            "/var/lib/plexor/volumes/vm-bad.qcow2",
            "br-default",
            cidataIsoPath: null);

        xml.ShouldContain("<vcpu>2</vcpu>");
        xml.ShouldContain("<memory>1048576</memory>");
    }

    [Fact(DisplayName = "Given Config with SshPublicKey, when BuildDomainXml, then the key is parsed (no XML side-effects — cidata ISO is wired in P1 #9)")]
    public void SshPublicKeyParsesWithoutError()
    {
        var spec = NewSpec("vm-key", /*lang=json,strict*/ """
            {
              "Vcpu": 2,
              "RamBytes": 2147483648,
              "SshPublicKey": "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAITESTKEY test@example.com",
              "SshKeyFingerprint": "SHA256:abc123def456"
            }
            """);

        // No exception; XML is valid; the SSH key isn't yet
        // wired into the XML itself (that's P1 #9 — CidataBuilder
        // produces an ISO we attach as cdrom).
        var xml = LibvirtKvmXmlBuilder.BuildDomainXml(
            spec,
            Guid.NewGuid(),
            "/var/lib/plexor/volumes/vm-key.qcow2",
            "br-default",
            cidataIsoPath: "/var/lib/plexor/cidata/vm-key-cidata.iso");

        xml.ShouldContain("device=\"cdrom\"");
    }

    [Fact(DisplayName = "Given default LibvirtKvmConfig ctor, then RamBytes=1GiB, Vcpu=2, DiskBytes=null")]
    public void DefaultConfigShape()
    {
        var cfg = new LibvirtKvmConfig();

        cfg.RamBytes.ShouldBe(1L * 1024 * 1024 * 1024);
        cfg.Vcpu.ShouldBe(2);
        cfg.DiskBytes.ShouldBeNull();
        cfg.NetworkName.ShouldBe("default");
        cfg.BaseImageRef.ShouldBeNull();
        cfg.SshPublicKey.ShouldBeNull();
        cfg.SshKeyFingerprint.ShouldBeNull();
    }

    [Fact(DisplayName = "Given the canonical wire-format JSON, when deserialised, then fields bind to Vcpu / DiskBytes / SshPublicKey")]
    public void WireFormatJsonBindsPascalCaseFields()
    {
        const string json = """
            {
              "Vcpu": 4,
              "RamBytes": 8589934592,
              "DiskBytes": 107374182400,
              "NetworkName": "br-tenant-a",
              "BaseImageRef": "ubuntu-22.04-cloud",
              "SshPublicKey": "ssh-ed25519 AAAA… user",
              "SshKeyFingerprint": "SHA256:abcd"
            }
            """;

        var cfg = JsonSerializer.Deserialize<LibvirtKvmConfig>(json);

        cfg.ShouldNotBeNull();
        cfg!.Vcpu.ShouldBe(4);
        cfg.RamBytes.ShouldBe(8589934592L);
        cfg.DiskBytes.ShouldBe(107374182400L);
        cfg.NetworkName.ShouldBe("br-tenant-a");
        cfg.BaseImageRef.ShouldBe("ubuntu-22.04-cloud");
        cfg.SshPublicKey.ShouldBe("ssh-ed25519 AAAA… user");
        cfg.SshKeyFingerprint.ShouldBe("SHA256:abcd");
    }

    private static WorkloadSpec NewSpec(string name, string configJson)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(configJson);
        return new WorkloadSpec(new WorkloadKind.Vm(), name, element);
    }
}
