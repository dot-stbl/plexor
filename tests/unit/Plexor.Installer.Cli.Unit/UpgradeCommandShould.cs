// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpgradeCommandShould — smoke test for the UpgradeCommand. Drives
// the command's ExecuteAsync on the validation-error path so the
// rest of the orchestrator (BinaryFetcher / AtomicSwap /
// MigratorRunner / HealthProbe) is not exercised on a non-Linux
// dev box.
//
// The unit-path + swap happy paths need real filesystem + systemd
// access (Linux-only); those land in an integration test suite in
// a later sprint.
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Plexor.Installer.Cli.Settings;
using Plexor.Installer.Commands;
using Shouldly;
using Xunit;

namespace Plexor.Installer.Cli.Unit;

public sealed class UpgradeCommandShould
{
    [Fact(DisplayName = "Given relative PLEXOR_DATA_PATH, when ExecuteAsync, then returns non-zero")]
    public async Task ReturnsNonZeroForInvalidLayoutAsync()
    {
        var original = Environment.GetEnvironmentVariable("PLEXOR_DATA_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", "relative/data");
            var command = new UpgradeCommand();

            var exit = await command.ExecuteAsync(null!, new UpgradeSettings { Source = "/tmp/nope" });

            exit.ShouldNotBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", original);
        }
    }

    [Fact(DisplayName = "Given --no-restart + valid layout + missing install, when ExecuteAsync on Linux, then returns not-installed")]
    public async Task ReturnsNotInstalledOnLinuxWithoutUnitAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var originalData = Environment.GetEnvironmentVariable("PLEXOR_DATA_PATH");
        var originalSystemd = Environment.GetEnvironmentVariable("PLEXOR_SYSTEMD_UNIT_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", null);
            Environment.SetEnvironmentVariable(
                "PLEXOR_SYSTEMD_UNIT_PATH",
                Path.Combine(Path.GetTempPath(), "plexor-upgrade-test-" + Guid.NewGuid().ToString("N")));

            var command = new UpgradeCommand();

            var exit = await command.ExecuteAsync(null!, new UpgradeSettings { Source = "/tmp/nope" });

            // Exit code 3 = "not installed" — proves we got past
            // path validation but hit the install-check.
            exit.ShouldBe(3);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", originalData);
            Environment.SetEnvironmentVariable("PLEXOR_SYSTEMD_UNIT_PATH", originalSystemd);
        }
    }
}
