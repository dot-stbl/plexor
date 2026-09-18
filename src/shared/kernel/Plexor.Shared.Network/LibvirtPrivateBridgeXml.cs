// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtPrivateBridgeXml — file-static XML builder for a private
// libvirt bridge. Pure function (no DI, no I/O); lives in its own
// file per the class-decomposition rule (file static > private
// helper). Mirrors LinuxBridgeBackendXml in the NodeAgent but
// parameterised on subnet so the host can pick non-overlapping
// per-cluster ranges.
//
// libvirt's built-in dnsmasq serves DNS + DHCP for the range;
// Plexor clusters don't run a host-side dnsmasq.
// ============================================================================

using System.Globalization;

namespace Plexor.Shared.Network;

/// <summary>
///     Pure-function builder for the libvirt network XML that
///     <see cref="LibvirtNetworkProvider.EnsurePrivateBridgeAsync" />
///     writes under <c>/etc/libvirt/qemu/networks/{name}.xml</c>.
/// </summary>
public static class LibvirtPrivateBridgeXml
{
    /// <summary>
    ///     Build the libvirt network XML for a private /24 bridge.
    /// </summary>
    /// <param name="name">Network name (matches <c>^[a-zA-Z0-9_-]{1,16}$</c>).</param>
    /// <param name="subnet">
    ///     Three-octet prefix; the gateway IP is
    ///     <c>{subnet}.1</c> and the DHCP range is
    ///     <c>{dhcpRange}.2 .. {dhcpRange}.254</c>.
    /// </param>
    /// <param name="dhcpRange">
    ///     Third octet for the high end of the DHCP range. Same
    ///     value as <paramref name="subnet" /> for a single-/24
    ///     pool; larger deployments may use different subnet /
    ///     dhcpRange pairs (e.g. <c>"10.42.0"</c> / <c>"10.42.1"</c>
    ///     to skip the gateway octet).
    /// </param>
    public static string BuildNetworkXml(string name, string subnet, string dhcpRange)
    {
        var gateway = string.Create(CultureInfo.InvariantCulture, $"{subnet}.1");
        var rangeStart = string.Create(CultureInfo.InvariantCulture, $"{subnet}.2");
        var rangeEnd = string.Create(CultureInfo.InvariantCulture, $"{dhcpRange}.254");

        return $"""
                 <network>
                   <name>{name}</name>
                   <bridge name="virbr{name}" stp="on" delay="0"/>
                   <ip address="{gateway}" netmask="255.255.255.0">
                     <dhcp>
                       <range start="{rangeStart}" end="{rangeEnd}"/>
                     </dhcp>
                   </ip>
                 </network>
                 """;
    }
}
