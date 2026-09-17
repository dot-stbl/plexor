// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodesAddCommand — `plx host nodes add`. Pre-register a node
// from the operator's host session. The CLI fills the registration
// payload (token + hostname + IP + role + zero hardware), calls
// `POST /api/v1/nodes/register`, and prints:
//
//   - the new node row (one-line summary)
//   - the node-bearer token (printed once; the operator pastes it
//     into the NodeAgent's install.sh)
//   - the cluster endpoint (mTLS + WireGuard rendezvous)
//
// The token printout is wrapped in a clear "sensitive — shown
// once" warning so it doesn't get lost in the scrollback.
// ============================================================================

using Plexor.Installer.Cli;
using Plexor.Installer.Cli.Refit;
using Plexor.Installer.Cli.Settings;
using Plexor.Shared.Console;
using Refit;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx host nodes add</c> — register a new node on the
///     resolved cluster. Calls
///     <c>POST /api/v1/nodes/register</c> via the Refit client and
///     surfaces the returned node-bearer token (sensitive;
///     shown once).
/// </summary>
public sealed class HostNodesAddCommand : AsyncCommand<HostNodesAddSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, HostNodesAddSettings settings)
    {
        var config = settings.ResolveConfig();
        if (config is null)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "host / token missing",
                "pass --host and --token, or set PLX_HOST + PLX_TOKEN, or write ~/.plx/config.json"));
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.Name) ||
            string.IsNullOrWhiteSpace(settings.Ip) ||
            string.IsNullOrWhiteSpace(settings.JoinToken))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "required args missing",
                "--name, --ip, and --join-token are all required"));
            return 3;
        }

        var role = ParseRole(settings.Role);

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            var request = new HostRegisterNodeRequest(
                settings.JoinToken,
                settings.Name,
                settings.Ip,
                role,
                Hardware: new HostNodeHardware(0, 0, 0, []),
                IsoVersion: "0.0.0-cli",
                WireguardPublicKey: string.Empty);
            var response = await client.RegisterNodeAsync(request, default);
            HostNodesAddRenderer.Render(response);
            return 0;
        }
        catch (ApiException ex)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                $"HTTP {(int)ex.StatusCode}",
                ex.Message));
            return 4;
        }
        finally
        {
            http.Dispose();
        }
    }

    private static HostNodeRole ParseRole(string? raw)
    {
        return raw?.ToLowerInvariant() switch
        {
            "control" => HostNodeRole.Control,
            "compute" => HostNodeRole.Compute,
            null or "" or "worker" => HostNodeRole.Compute,
            _ => HostNodeRole.Compute
        };
    }
}

/// <summary>
///     File-static renderer for <see cref="HostNodesAddCommand" />.
///     Pulled out per code-shape.md §1a (no private methods on
///     production classes).
/// </summary>
file static class HostNodesAddRenderer
{
    /// <summary>
    ///     Print the new node row + the node-bearer token + the
    ///     cluster endpoint. The token is wrapped in a banner
    ///     warning so it stands out in scrollback and the
    ///     operator pastes it before the terminal scrolls.
    /// </summary>
    /// <param name="response">Register response from the host.</param>
    public static void Render(HostRegisterNodeResponse response)
    {
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
            $"{BannerArt.Icon.Status} registered {response.Node.Hostname} ({response.Node.Id})"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  role:    {response.Node.Role.ToString().ToLowerInvariant()}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  status:  {response.Node.Status.ToString().ToLowerInvariant()}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  endpoint: {response.ClusterEndpoint}"));

        AnsiConsole.WriteLine();
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Warn(
            $"{BannerArt.Icon.Warn} sensitive — shown once"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.B("NODE BEARER TOKEN"));
        AnsiConsole.Console.WriteLine(response.NodeToken);
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            "  paste into the NodeAgent's install.sh before it heartbeats"));
    }
}