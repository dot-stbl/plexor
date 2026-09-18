// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// INetworkProvider — host-side, cluster-provisioning surface for
// the node's libvirt `virsh net-*` commands. Sits beside the
// per-node INetworkBackend (Plexor.Shared.Compute) which handles
// runtime attach/detach; this interface is what the control plane
// uses to inspect and provision networks BEFORE workloads land.
//
// v0.1 minimal contract (issue #13):
//   - ListNetworksAsync      : virsh net-list
//   - ResolveBridgeNameAsync : pick a default bridge ("br0" or
//                              "default" if no other bridge exists)
//   - EnsurePrivateBridgeAsync (issue #14): virsh net-define +
//      net-start + net-autostart a fresh bridge with a /24 subnet
//      and a DHCP range.
//
// Linux-only by design (libvirt is Linux-only). The libvirt
// implementation guards itself via OperatingSystem.IsLinux() so
// unit-test hosts (Windows CI) fail-fast instead of throwing.
// ============================================================================

namespace Plexor.Shared.Network;

/// <summary>
///     Host-side port to the libvirt network surface. Implementations
///     shell out to <c>virsh</c> on a node (v0.1) or call a
///     libvirt client library (v0.2+).
/// </summary>
public interface INetworkProvider
{
    /// <summary>
    ///     List every libvirt network defined on the node, in
    ///     name order. Includes auto-started + transient networks
    ///     (matches <c>virsh net-list --all</c> semantics).
    ///     Returns an empty list when no networks are defined
    ///     (a fresh libvirtd starts with only the built-in
    ///     <c>default</c> network, which appears here).
    /// </summary>
    /// <param name="cancellationToken">Forwarded to the virsh invocation.</param>
    public Task<IReadOnlyList<string>> ListNetworksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pick a sensible default bridge for the operator's VMs.
    ///     Resolution order:
    ///     <list type="number">
    ///         <item>The <c>default</c> libvirt network if it exists and is active.</item>
    ///         <item>Otherwise, the first user-defined bridge that is active.</item>
    ///         <item>Otherwise <c>null</c> — the operator must define one first.</item>
    ///     </list>
    /// </summary>
    /// <param name="cancellationToken">Forwarded to the virsh invocations.</param>
    public Task<string?> ResolveBridgeNameAsync(CancellationToken cancellationToken = default);
}