// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmRuntimeConfig — the runtime knobs the operator asks for when
// provisioning a VM. Wire-stable shape: this is what the host sends
// to the NodeAgent inside the per-workload-create envelope, and what
// the libvirt KVM/QEMU provider translates into <domain> XML on the
// node side.
//
// Not to be confused with WorkloadSpec — WorkloadSpec is the wire
// envelope (kind / name / opaque JSON config); VmRuntimeConfig is
// the typed shape of that JSON config when the kind is "vm".
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     The runtime configuration of a single VM the operator wants
///     provisioned. Serialized verbatim into the
///     <c>workload.create</c> command envelope's
///     <c>payloadJson</c> for the assigned NodeAgent; the libvirt
///     KVM/QEMU provider deserializes it and translates each field
///     into its matching <c>&lt;domain&gt;</c> XML element.
///     Validated by <see cref="VmRuntimeConfigValidator" /> before
///     it leaves the control plane.
/// </summary>
/// <param name="Vcpu">vCPU count (1..256).</param>
/// <param name="RamBytes">Memory in bytes (512 MiB..1 TiB).</param>
/// <param name="DiskBytes">Root disk size in bytes (1 GiB..10 TiB).</param>
/// <param name="ImageRef">
///     Image ref from the image catalog (e.g.
///     <c>"ubuntu-22.04-cloud"</c>). Must match a registered
///     <c>IImageRegistry</c> on the assigned node; the libvirt
///     provider clones it into the workload's root volume.
/// </param>
/// <param name="NetworkName">
///     Bridge to attach the VM's NIC to. Null = operator-default
///     bridge (the libvirt <c>default</c> network). Must match
///     <c>^[a-zA-Z0-9_-]{1,16}$</c> when set.
/// </param>
/// <param name="SshKeyFingerprint">
///     Optional SSH key fingerprint (the control plane's
///     <c>sigil.ssh_keys</c> table). When set, the NodeAgent
///     injects the public key into the cloud-init user-data so the
///     operator can SSH in on first boot.
/// </param>
public sealed record VmRuntimeConfig(
    int Vcpu,
    long RamBytes,
    long DiskBytes,
    string ImageRef,
    string? NetworkName,
    string? SshKeyFingerprint = null);