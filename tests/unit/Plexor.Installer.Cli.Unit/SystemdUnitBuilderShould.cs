// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SystemdUnitBuilderShould — unit tests for the SystemdUnitBuilder
// template. Pure string render — no I/O.
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Shouldly;
using Xunit;

namespace Plexor.Installer.Cli.Unit;

public sealed class SystemdUnitBuilderShould
{
    [Fact(DisplayName = "Given paths + description, when Build, then renders all sections")]
    public void BuildRendersAllSections()
    {
        var unit = SystemdUnitBuilder.Build(
            "Plexor.Host (cluster prod)",
            "/var/lib/plexor/bin/plexor-host",
            "/var/lib/plexor");

        unit.ShouldContain("[Unit]");
        unit.ShouldContain("Description=Plexor.Host (cluster prod)");
        unit.ShouldContain("After=network-online.target");
        unit.ShouldContain("[Service]");
        unit.ShouldContain("Type=simple");
        unit.ShouldContain("User=root");
        unit.ShouldContain("WorkingDirectory=/var/lib/plexor");
        unit.ShouldContain("ExecStart=/var/lib/plexor/bin/plexor-host run");
        unit.ShouldContain("Environment=PLEXOR_DATA_PATH=/var/lib/plexor");
        unit.ShouldContain("Restart=on-failure");
        unit.ShouldContain("[Install]");
        unit.ShouldContain("WantedBy=multi-user.target");
    }
}
