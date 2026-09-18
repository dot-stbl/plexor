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

    /// <summary>
    ///     Define and start a private bridge on the node if one
    ///     with <paramref name="name" /> doesn't already exist.
    ///     Idempotent: an existing bridge of the same name is left
    ///     alone (no re-define, no restart). The bridge gets a
    ///     /24 subnet (<paramref name="subnet" />.0/24) with a
    ///     DHCP range <paramref name="subnet" />.2..<paramref name="dhcpRange" />.254
    ///     and libvirt's built-in dnsmasq serves DNS + DHCP.
    /// </summary>
    /// <param name="name">
    ///     Network name (must match <c>^[a-zA-Z0-9_-]{1,16}$</c>;
    ///     the underlying bridge device is named <c>virbr{name}</c>).
    /// </param>
    /// <param name="subnet">
    ///     /24 prefix; only the first three octets are used
    ///     (e.g. <c>"10.42.0"</c> → <c>10.42.0.0/24</c>).
    /// </param>
    /// <param name="dhcpRange">
    ///     Third octet of the DHCP range's last address. With
    ///     <paramref name="subnet" /> = <c>"10.42.0"</c> and
    ///     <paramref name="dhcpRange" /> = <c>"10.42.0"</c>, the
    ///     range is <c>10.42.0.2 .. 10.42.0.254</c>.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     True when the network was newly defined; false when it
    ///     already existed (idempotent path).
    /// </returns>
    public Task<bool> EnsurePrivateBridgeAsync(
        string name,
        string subnet,
        string dhcpRange,
        CancellationToken cancellationToken = default);
}
