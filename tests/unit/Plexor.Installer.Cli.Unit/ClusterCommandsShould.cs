// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterCommandsShould — smoke tests for the `plx cluster *`
// command set. Each test exercises the validation-error path
// (no host/token resolved) so we don't hit the host's HTTP
// surface from the dev-box test runner.
//
// Full happy-path tests (mocked IPlexorHostApi, asserting each
// Refit method is invoked with the expected arguments) live in a
// later sprint — they require a constructor seam on
// HostApiClientFactory to inject the substitute without standing
// up a real HTTP server.
// ============================================================================

using Plexor.Installer.Cli.Settings;
using Plexor.Installer.Commands;
using Shouldly;
using Xunit;

namespace Plexor.Installer.Cli.Unit;

public sealed class ClusterCommandsShould
{
    [Fact(DisplayName = "Given no host/token, when ClusterListCommand.ExecuteAsync, then returns exit 2")]
    public async Task ClusterListReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new ClusterListCommand().ExecuteAsync(null!, new ClusterListSettings());

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when ClusterCreateCommand.ExecuteAsync, then returns exit 2")]
    public async Task ClusterCreateReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new ClusterCreateCommand().ExecuteAsync(null!, new ClusterCreateSettings());

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when ClusterShowCommand.ExecuteAsync, then returns exit 2")]
    public async Task ClusterShowReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new ClusterShowCommand().ExecuteAsync(null!, new ClusterShowSettings { ClusterId = "cluster_01H" });

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when ClusterDeleteCommand.ExecuteAsync, then returns exit 2")]
    public async Task ClusterDeleteReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new ClusterDeleteCommand().ExecuteAsync(null!, new ClusterDeleteSettings { ClusterId = "cluster_01H" });

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given no host/token, when ClusterRotateTokenCommand.ExecuteAsync, then returns exit 2")]
    public async Task ClusterRotateTokenReturnsTwoOnMissingConfigAsync()
    {
        var exit = await new ClusterRotateTokenCommand().ExecuteAsync(null!, new ClusterRotateTokenSettings { ClusterId = "cluster_01H" });

        exit.ShouldBe(2);
    }

    [Fact(DisplayName = "Given --page=0, when ClusterListCommand.ExecuteAsync, then returns exit 3 (invalid page)")]
    public async Task ClusterListReturnsThreeOnInvalidPageAsync()
    {
        var command = new ClusterListCommand();
        var settings = new ClusterListSettings
        {
            Host = "https://plexor.example.com",
            Token = "test-token",
            Page = 0
        };

        var exit = await command.ExecuteAsync(null!, settings);

        exit.ShouldBe(3);
    }

    [Fact(DisplayName = "Given no --name, when ClusterCreateCommand.ExecuteAsync, then returns exit 3 (name missing)")]
    public async Task ClusterCreateReturnsThreeOnMissingNameAsync()
    {
        var command = new ClusterCreateCommand();
        var settings = new ClusterCreateSettings
        {
            Host = "https://plexor.example.com",
            Token = "test-token"
        };

        var exit = await command.ExecuteAsync(null!, settings);

        exit.ShouldBe(3);
    }
}