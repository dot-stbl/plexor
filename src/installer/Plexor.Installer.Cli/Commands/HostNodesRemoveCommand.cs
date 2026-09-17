// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodesRemoveCommand — `plx host nodes remove <nodeId>`.
// Calls `DELETE /api/v1/nodes/{id}` on the resolved host and
// prints a confirmation line. Destructive — requires
// confirmation unless `--yes` is passed.
//
// The v0.1 host controller does not yet expose the DELETE
// endpoint; the command translates a 404 from the host into a
// clear "not supported on this host version" message rather than
// failing silently.
// ============================================================================

using Plexor.Installer.Cli;
using Plexor.Installer.Cli.Settings;
using Plexor.Shared.Console;
using Refit;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx host nodes remove &lt;nodeId&gt;</c> — unregister a
///     node on the resolved cluster. Calls
///     <c>DELETE /api/v1/nodes/{nodeId}</c> via the Refit client.
/// </summary>
public sealed class HostNodesRemoveCommand : AsyncCommand<HostNodesRemoveSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, HostNodesRemoveSettings settings)
    {
        var config = settings.ResolveConfig();
        if (config is null)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "host / token missing",
                "pass --host and --token, or set PLX_HOST + PLX_TOKEN, or write ~/.plx/config.json"));
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.NodeId))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "node id missing",
                "pass the node id as the positional argument"));
            return 3;
        }

        if (!settings.Yes)
        {
            var confirm = AnsiConsole.Console.Prompt(
                new ConfirmationPrompt(
                    MarkupExtensions.Warn(
                        $"unregister node {settings.NodeId} on {config.Host}?")
                    + " [y/N]"));
            if (!confirm)
            {
                AnsiConsole.Console.MarkupLine(ErrorFormatter.Info("cancelled", "no changes made"));
                return 0;
            }
        }

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            await client.DeleteNodeAsync(settings.NodeId, default);
            AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
                $"{BannerArt.Icon.Ok} unregistered {settings.NodeId}"));
            return 0;
        }
        catch (ApiException ex) when ((int)ex.StatusCode == 404)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "DELETE not supported on this host version",
                "the v0.1 host relies on the heartbeat evaluator to flip nodes to Gone after 90 s of silence; a DELETE endpoint ships in a later release"));
            return 5;
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
}