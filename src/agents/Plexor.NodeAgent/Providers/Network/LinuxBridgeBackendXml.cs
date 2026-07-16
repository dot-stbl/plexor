// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LinuxBridgeBackendXml — file-static XML builder for libvirt
// network definitions. Pure function (no DI, no I/O); lives in
// its own file per the class-decomposition rule (private helpers
// > file static class).
// ==========================================================================

namespace Plexor.NodeAgent.Providers.Network;

internal static class LinuxBridgeBackendXml
{
    /// <summary>
    ///     Build a libvirt network XML for a Linux bridge.
    ///     Minimal: NAT'd bridge with DHCP. The bridge device is
    ///     named <c>br-{name}</c> on the host; libvirt creates
    ///     it on net-start.
    /// </summary>
    /// <param name="name">Network name (must be DNS-safe + libvirt-compliant).</param>
    public static string BuildNetworkXml(string name)
    {
        return $"""
                 <network>
                   <name>{name}</name>
                   <bridge name="br-{name}" stp="on" delay="0"/>
                   <forward mode="nat"/>
                   <ip address="192.168.254.1" netmask="255.255.255.0">
                     <dhcp>
                       <range start="192.168.254.2" end="192.168.254.254"/>
                     </dhcp>
                   </ip>
                 </network>
                 """;
    }
}
