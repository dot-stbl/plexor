// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtNetListParserShould — unit tests for the virsh net-list
// stdout parser. Pure function, runs everywhere — no libvirt
// dependency. Integration coverage against a real libvirtd lives
// in the integration test suite.
// ==========================================================================

using Plexor.Shared.Network;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Network.Unit;

public sealed class LibvirtNetListParserShould
{
    [Fact(DisplayName = "Given empty input, when ParseNames, then returns empty list")]
    public void EmptyInputReturnsEmpty()
    {
        LibvirtNetListParser.ParseNames("").ShouldBeEmpty();
    }

    [Fact(DisplayName = "Given whitespace-only input, when ParseNames, then returns empty list")]
    public void WhitespaceOnlyInputReturnsEmpty()
    {
        LibvirtNetListParser.ParseNames("   \r\n\t\n").ShouldBeEmpty();
    }

    [Fact(DisplayName = "Given the canonical virsh net-list table, when ParseNames, then returns only network names")]
    public void CanonicalTableReturnsNames()
    {
        const string Stdout = """
                              Name                 State      Autostart     Persistent
                              ----------------------------------------------------------
                              default              active     yes           yes
                              plexor-prod-vpc      active     yes           yes
                              plexor-staging       inactive   no            yes
                              """;

        var names = LibvirtNetListParser.ParseNames(Stdout);

        names.ShouldBe(["default", "plexor-prod-vpc", "plexor-staging"], ignoreOrder: false);
    }

    [Fact(DisplayName = "Given a single network, when ParseNames, then returns that one name")]
    public void SingleNetworkReturnsOne()
    {
        const string Stdout = """
                              Name       State    Autostart   Persistent
                              ----------------------------------------------
                              default    active   yes         yes
                              """;

        var names = LibvirtNetListParser.ParseNames(Stdout);

        names.ShouldBe(["default"]);
    }

    [Fact(DisplayName = "Given header only (no rows), when ParseNames, then returns empty list")]
    public void HeaderOnlyReturnsEmpty()
    {
        const string Stdout = """
                              Name       State    Autostart   Persistent
                              ----------------------------------------------
                              """;

        LibvirtNetListParser.ParseNames(Stdout).ShouldBeEmpty();
    }

    [Fact(DisplayName = "Given table rule with ==== style, when ParseNames, then skips the rule")]
    public void EqualsRuleIsSkipped()
    {
        const string Stdout = """
                              Name       State    Autostart   Persistent
                              ==============================================
                              default    active   yes         yes
                              """;

        var names = LibvirtNetListParser.ParseNames(Stdout);

        names.ShouldBe(["default"]);
    }

    [Fact(DisplayName = "Given blank lines mixed with rows, when ParseNames, then blank lines are ignored")]
    public void BlankLinesAreIgnored()
    {
        const string Stdout = """

                              Name       State    Autostart   Persistent
                              ----------------------------------------------

                              default    active   yes         yes

                              plexor-x   active   yes         yes
                              """;

        var names = LibvirtNetListParser.ParseNames(Stdout);

        names.ShouldBe(["default", "plexor-x"]);
    }

    [Fact(DisplayName = "Given trailing CR characters, when ParseNames, then handles line endings correctly")]
    public void CrLfLineEndingsAreHandled()
    {
        const string Stdout =
            "Name       State    Autostart   Persistent\r\n" +
            "----------------------------------------------\r\n" +
            "default    active   yes         yes\r\n" +
            "plexor-x   active   yes         yes\r\n";

        var names = LibvirtNetListParser.ParseNames(Stdout);

        names.ShouldBe(["default", "plexor-x"]);
    }
}