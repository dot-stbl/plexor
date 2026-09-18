// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtPrivateBridgeXmlShould — unit tests for the libvirt network
// XML builder. Pure function — runs everywhere. The OS-guard +
// real virsh invocation is covered by the integration suite.
// ==========================================================================

using Shouldly;
using Xunit;

namespace Plexor.Shared.Network.Unit;

public sealed class LibvirtPrivateBridgeXmlShould
{
    [Fact(DisplayName = "Given a /24 subnet, when BuildNetworkXml, then embeds the expected name + bridge + gateway + DHCP range")]
    public void CanonicalBuildEmbedsAllFields()
    {
        var xml = LibvirtPrivateBridgeXml.BuildNetworkXml("prod", "10.42.0", "10.42.0");

        xml.ShouldContain("<name>prod</name>");
        xml.ShouldContain("<bridge name=\"virbrprod\"");
        xml.ShouldContain("<ip address=\"10.42.0.1\" netmask=\"255.255.255.0\">");
        xml.ShouldContain("<range start=\"10.42.0.2\" end=\"10.42.0.254\"/>");
    }

    [Fact(DisplayName = "Given split subnet/dhcpRange, when BuildNetworkXml, then DHCP range uses dhcpRange")]
    public void SplitSubnetAndRangePreservesBoundary()
    {
        var xml = LibvirtPrivateBridgeXml.BuildNetworkXml("vpc-a", "10.42.0", "10.42.1");

        xml.ShouldContain("<ip address=\"10.42.0.1\"");
        xml.ShouldContain("<range start=\"10.42.0.2\" end=\"10.42.1.254\"/>");
    }

    [Fact(DisplayName = "Given a name with hyphens, when BuildNetworkXml, then the bridge name preserves the prefix")]
    public void HyphenatedNamePreservesPrefix()
    {
        var xml = LibvirtPrivateBridgeXml.BuildNetworkXml("prod-vpc", "10.42.0", "10.42.0");

        xml.ShouldContain("virbrprod-vpc");
    }

    [Fact(DisplayName = "Given any input, when BuildNetworkXml, then wraps in a single <network> root element")]
    public void SingleNetworkRootElement()
    {
        var xml = LibvirtPrivateBridgeXml.BuildNetworkXml("prod", "10.42.0", "10.42.0");

        xml.TrimStart().ShouldStartWith("<network>");
        xml.TrimEnd().ShouldEndWith("</network>");
    }
}
