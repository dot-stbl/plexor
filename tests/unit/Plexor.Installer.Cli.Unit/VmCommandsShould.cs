// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmCommandsShould — smoke tests for the `plx vm *` command set.
// Each test exercises the validation-error path (no host/token
// resolved, or required arg missing) so we don't hit the host's HTTP
// surface from the dev-box test runner.
//
// Full happy-path tests (mocked IPlexorHostApi, asserting each
// Refit method is invoked with the expected arguments) live in a
// later sprint — they require a constructor seam on
// HostApiClientFactory to inject the substitute without standing up
// a real HTTP server.
// ============================================================================

using Plexor.Installer.Cli.Settings;
using Plexor.Installer.Commands;
using Shouldly;
using Xunit;

namespace Plexor.Installer.Cli.Unit;

public sealed class VmCommandsShould
{
    [Fact(DisplayName = "Given no host/token, when VmListCommand.ExecuteAsync, then returns exit 2")]
    public async Task VmListReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new VmListCommand().ExecuteAsync(null!, new VmListSettings());

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when VmCreateCommand.ExecuteAsync, then returns exit 2")]
    public async Task VmCreateReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new VmCreateCommand().ExecuteAsync(null!, new VmCreateSettings());

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when VmStartCommand.ExecuteAsync, then returns exit 2")]
    public async Task VmStartReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new VmStartCommand().ExecuteAsync(null!, new VmStartSettings { VmId = "wl_01H" });

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when VmStopCommand.ExecuteAsync, then returns exit 2")]
    public async Task VmStopReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new VmStopCommand().ExecuteAsync(null!, new VmStopSettings { VmId = "wl_01H" });

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when VmDeleteCommand.ExecuteAsync, then returns exit 2")]
    public async Task VmDeleteReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new VmDeleteCommand().ExecuteAsync(null!, new VmDeleteSettings { VmId = "wl_01H", Yes = true });

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given --cluster but no --name / --image, when VmCreateCommand.ExecuteAsync, then returns exit 3")]
    public async Task VmCreateReturnsThreeOnMissingArgsAsync()
    {
        var command = new VmCreateCommand();
        var settings = new VmCreateSettings
        {
            Host = "https://plexor.example.com",
            Token = "test-token",
            Cluster = "cluster_01H"
        };

        var exit = await command.ExecuteAsync(null!, settings);

        exit.ShouldBe(3);
    }

    [Fact(DisplayName = "Given --cluster but no vm-id positional, when VmStartCommand.ExecuteAsync, then returns exit 3")]
    public async Task VmStartReturnsThreeOnMissingVmIdAsync()
    {
        var command = new VmStartCommand();
        var settings = new VmStartSettings
        {
            Host = "https://plexor.example.com",
            Token = "test-token",
            Cluster = "cluster_01H"
        };

        var exit = await command.ExecuteAsync(null!, settings);

        exit.ShouldBe(3);
    }
}
