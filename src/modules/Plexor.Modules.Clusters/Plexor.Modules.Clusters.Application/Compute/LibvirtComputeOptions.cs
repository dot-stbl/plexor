// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtComputeOptions — IOptions-bound configuration for the libvirt
// IComputeProvider implementation. Owned by the Clusters Application
// layer (the only layer that surfaces runtime knobs to the host) and
// bound from the [Clusters:Compute:Libvirt] section of plexor.yaml /
// PLX_CLUSTERS_COMPUTE_LIBVIRT_* env vars by the composition root.
//
// Presence semantics. The host's DI registration inspects this
// section's existence (configuration.GetSection(...).Exists()) to
// decide between LibvirtComputeProvider and the NoOp stub. The
// absence of the section keeps NoOp as the default for dev /
// self-hosted deployments that don't run a libvirt daemon.
//
// Validation. Range attributes are enforced by
// ValidateDataAnnotations() chained with ValidateOnStart() in the
// composition root — a bad TimeoutSeconds fails the host startup,
// not the first VM provision.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace Plexor.Modules.Clusters.Application.Compute;

/// <summary>
///     Runtime configuration for the libvirt IComputeProvider
///     implementation. Bound from the <c>[Clusters:Compute:Libvirt]</c>
///     TOML section; absence of the section keeps the NoOp default.
/// </summary>
/// <remarks>
///     <para><b>Why a nested section.</b> The cluster module owns
///     several provider seams (libvirt for VMs, future docker-compose
///     / k3s / cloud adapters). A nested <c>Compute:Libvirt</c> shape
///     gives each provider its own section without polluting the
///     top-level <c>Clusters</c> namespace.</para>
///     <para><b>Why no required attribute on ConnectionUri.</b>
///     A default of <c>qemu:///system</c> matches the local-daemon
///     case where an operator wires libvirt on the same host as
///     Plexor and doesn't bother with a custom URI. The
///     ValidateDataAnnotations + ValidateOnStart guard still
///     refuses an empty string after binding.</para>
/// </remarks>
public sealed class LibvirtComputeOptions
{
    /// <summary>
    ///     Config section name; matches <c>plexor.yaml</c> →
    ///     <c>[Clusters.Compute.Libvirt]</c> or
    ///     <c>PLX_CLUSTERS_COMPUTE_LIBVIRT_*</c> env vars. The
    ///     presence of the section (not its contents) is what
    ///     selects the libvirt provider over NoOp.
    /// </summary>
    public const string SectionName = "Clusters:Compute:Libvirt";

    /// <summary>
    ///     virsh connection URI passed as <c>-c</c>. Default
    ///     <c>qemu:///system</c> matches the local-system libvirt
    ///     daemon most common on bare-metal hosts. Switch to
    ///     <c>qemu+ssh://user@host/system</c> for a remote node.
    /// </summary>
    [Required]
    public string ConnectionUri { get; init; } = "qemu:///system";

    /// <summary>
    ///     Default libvirt storage pool name (e.g. <c>default</c>,
    ///     <c>plexor</c>). Reserved for future use — v0.1 passes
    ///     disk paths directly via <c>--disk</c> and doesn't touch
    ///     the default pool.
    /// </summary>
    public string StoragePool { get; init; } = "default";

    /// <summary>
    ///     Default virtual network name. Reserved for future use —
    ///     v0.1 attaches the VM's NICs to whatever networks the
    ///     workload request names.
    /// </summary>
    public string DefaultNetwork { get; init; } = "default";

    /// <summary>
    ///     Path to the <c>virsh</c> binary. Default <c>virsh</c>
    ///     (resolved via the host's <c>PATH</c>). Override for
    ///     non-standard installs (<c>/usr/local/bin/virsh</c>,
    ///     <c>/opt/libvirt/bin/virsh</c>).
    /// </summary>
    public string VirshPath { get; init; } = "virsh";

    /// <summary>
    ///     Timeout per <c>virsh</c> invocation, in seconds.
    ///     Default 60; lower bound 5 (a sub-5s timeout trips on
    ///     healthy networks during disk-attached create calls);
    ///     upper bound 600 (10 minutes — anything longer is a
    ///     provision failure, not a slow call).
    /// </summary>
    [Range(5, 600)]
    public int TimeoutSeconds { get; init; } = 60;
}
