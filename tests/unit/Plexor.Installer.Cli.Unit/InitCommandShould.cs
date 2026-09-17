// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InitCommandShould — smoke test for the InitCommand. Drives the
// command's ExecuteAsync on the validation-error path so the rest
// of the orchestrator (ProgressRunner / filesystem / systemd) is
// not exercised on a non-Linux dev box.
//
// The unit-path + data-path happy paths need real /var/lib/plexor
// write access (Linux-only); those land in an integration test
// suite in a later sprint.
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Plexor.Installer.Cli.Settings;
using Plexor.Installer.Commands;
using Shouldly;
using Xunit;

namespace Plexor.Installer.Cli.Unit;

public sealed class InitCommandShould
{
    [Fact(DisplayName = "Given relative PLEXOR_DATA_PATH, when ExecuteAsync, then returns non-zero")]
    public async Task ReturnsNonZeroForInvalidLayoutAsync()
    {
        var original = Environment.GetEnvironmentVariable("PLEXOR_DATA_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", "relative/data");
            var command = new InitCommand();

            var exit = await command.ExecuteAsync(null!, new InitSettings());

            exit.ShouldNotBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLEXOR_DATA_PATH", original);
        }
    }

    [Fact(DisplayName = "Given default env, when ExecuteAsync on Linux, then reaches binary-resolution step")]
    public async Task ReachesBinaryResolutionOnDefaultLayoutAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var command = new InitCommand();
        var exit = await command.ExecuteAsync(null!, new InitSettings { NoStart = true });

        // Exit code 4 = "source binary not found" (the test runner
        // doesn't expose a stable executable path); reaching this
        // step proves the layout validation + pre-flight passed.
        exit.ShouldBeOneOf(0, 4);
    }
}
