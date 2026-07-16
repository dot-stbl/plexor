// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// INetworkBackend — topology abstraction. A network is a virtual
// switch the VM provider attaches the workload's NIC to. The
// backend decides HOW the network lives (Linux bridge with
// dnsmasq, OVS bridge, OVN logical switch, macvlan parent) and
// exposes an opaque handle the VM provider references in its
// <interface> element.
//
// One backend per networking technology. The NodeAgent registers
// the backends it has configured (LinuxBridgeBackend for plain
// host bridges; OvsBridgeBackend for OVS-managed; OvnBackend for
// OVN-controller-attached). The IWorkloadProvider asks DI for the
// backend matching the requested NetworkSpec.
// ==========================================================================

namespace Plexor.Shared.Compute;

/// <summary>
///     Per-node network topology backend. A backend owns the
///     lifetime of the network interfaces it creates.
/// </summary>
public interface INetworkBackend
{
    /// <summary>
    ///     Provision a network interface for the workload and
    ///     return the backend-specific handle the VM provider
    ///     references in its <c>&lt;interface&gt;</c> section.
    ///     Idempotent on a re-attach (same name = same interface).
    /// </summary>
    /// <param name="networkSpec">
    ///     Logical name + kind. The backend ignores fields it
    ///     doesn't understand (so adding a new
    ///     <see cref="NetworkKind" /> is a contract change).
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Opaque handle. For LinuxBridgeBackend this is the
    ///     bridge name (e.g. <c>"br-prod-vpc"</c>); for OvsBridgeBackend
    ///     the OVS bridge name; for OvnBackend an OVN logical switch
    ///     port UUID. The VM provider does not interpret the handle
    ///     — it only stores it on the workload and passes it back
    ///     on detach.
    /// </returns>
    public Task<NetworkInterfaceHandle> AttachAsync(NetworkSpec networkSpec, CancellationToken cancellationToken);

    /// <summary>
    ///     Detach the interface from the workload and free the
    ///     backend resource. Idempotent — detaching a missing
    ///     interface is a successful no-op.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="cancellationToken"></param>
    public Task DetachAsync(NetworkInterfaceHandle handle, CancellationToken cancellationToken);
}

/// <summary>
///     Spec for a single network attach. The backend interprets
///     the fields it understands and ignores the rest.
/// </summary>
/// <param name="Name">
///     Logical name of the network (matches the cluster's
///     VPC name in the control plane — e.g. <c>"prod-vpc"</c>).
///     Used as the basis for the backend's storage identifier.
/// </param>
/// <param name="Kind">Networking technology the backend should use.</param>
public sealed record NetworkSpec(
    string Name,
    NetworkKind Kind);

/// <summary>
///     Networking technologies the backend may produce. Closed
///     set — adding a kind is a contract change.
/// </summary>
public enum NetworkKind
{
    /// <summary>
    ///     Linux kernel bridge with libvirt's <c>default</c>
    ///     dnsmasq setup. Simplest topology — VM gets a NAT'd
    ///     address on the bridge, host does the routing.
    /// </summary>
    LinuxBridge = 0,

    /// <summary>
    ///     OVS-managed bridge. Operator-managed; gives access to
    ///     VLAN tagging, port mirroring, OpenFlow.
    /// </summary>
    OvsBridge = 1,

    /// <summary>
    ///     OVN logical switch via the local ovn-controller. For
    ///     multi-tenant isolation: each tenant gets its own
    ///     logical switch, traffic between switches goes through
    ///     logical routers.
    /// </summary>
    OvnLogicalSwitch = 2,

    /// <summary>
    ///     macvlan interface on the host. Bypasses the bridge —
    ///     the VM appears on the host's L2 segment with its own
    ///     MAC. Used when the operator wants the VM directly
    ///     addressable from the LAN.
    /// </summary>
    MacVlan = 3
}

/// <summary>
///     Opaque network interface handle issued by an
///     <see cref="INetworkBackend" />. The backend name plus the
///     backend-specific reference identify the interface uniquely.
/// </summary>
/// <param name="BackendName">
///     Stable name of the backend that issued the handle
///     (e.g. <c>"linux-bridge"</c>, <c>"ovs-bridge"</c>). Used
///     for diagnostics and for routing back to the right backend
///     on detach.
/// </param>
/// <param name="Reference">
///     Backend-specific identifier. For LinuxBridgeBackend the
///     bridge name; for OvsBridgeBackend the OVS bridge name; for
///     OvnBackend the logical-switch-port UUID. The VM provider
///     does not parse this — only the issuing backend does.
/// </param>
public sealed record NetworkInterfaceHandle(string BackendName, string Reference);
