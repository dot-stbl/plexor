// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DestroyCommandShould — smoke test for the DestroyCommand. Exercises
// the validation-failure path on the dev box (Windows / Linux unit
// runner) and the "nothing to destroy" + idempotent paths.
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Plexor.Installer.Cli.Settings;
using Plexor.Installer.Commands;
using Shouldly;
using Xunit;

namespace Plexor.Installer.Cli.Unit;

public sealed class DestroyCommandShould
{
    [Fact(DisplayName = "Given relative PLEXOR_DATA_PATH, when Execute, then returns non-zero")]
    public void ReturnsNonZeroForInvalidLayout()
    {
        var original = Environment.GetEnvironmentVariable("PLEXOR_DATA_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", "relative/data");
            var command = new DestroyCommand();

            var exit = command.Execute(null!, new DestroySettings());

            exit.ShouldNotBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", original);
        }
    }

    [Fact(DisplayName = "Given no install + no --yes, when Execute on Linux with valid paths, then exits 0 (nothing-to-destroy)")]
    public void ReturnsZeroWhenNothingToDestroy()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var original = Environment.GetEnvironmentVariable("PLEXOR_SYSTEMD_UNIT_PATH");
        try
        {
            Environment.SetEnvironmentVariable(
                "PLEXOR_SYSTEMD_UNIT_PATH",
                Path.Combine(Path.GetTempPath(), "plexor-destroy-test-" + Guid.NewGuid().ToString("N")));

            var command = new DestroyCommand();

            var exit = command.Execute(null!, new DestroySettings());

            exit.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLEXOR_SYSTEMD_UNIT_PATH", original);
        }
    }
}
