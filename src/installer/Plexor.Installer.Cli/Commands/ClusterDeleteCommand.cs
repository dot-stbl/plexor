// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterDeleteCommand — `plx cluster delete <cluster-id>`.
// Calls `DELETE /api/v1/compute/clusters/{id}` on the host and
// prints a confirmation line. Destructive — requires
// confirmation unless `--yes` is passed.
//
// The `--purge` flag is accepted but a no-op in v0.1: the host's
// controller currently soft-deletes (cascades Node.Status = Gone
// only). A Phase 5+ follow-up extends the controller with a
// hard-delete variant that drops the cluster row + nodes
// entirely; the CLI flag is wired forward so the operator's
// script stays stable across the upgrade.
// ============================================================================

using Plexor.Installer.Cli;
using Plexor.Installer.Cli.Settings;
using Plexor.Shared.Console;
using Refit;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx cluster delete &lt;cluster-id&gt;</c> — soft-delete a
///     cluster. Calls <c>DELETE /api/v1/compute/clusters/{id}</c>
///     via the Refit client. Requires confirmation by default;
///     <c>--yes</c> skips the prompt (CI / scripted use).
/// </summary>
public sealed class ClusterDeleteCommand : AsyncCommand<ClusterDeleteSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ClusterDeleteSettings settings)
    {
        var config = settings.ResolveConfig();
        if (config is null)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "host / token missing",
                "pass --host and --token, or set PLX_HOST + PLX_TOKEN, or write ~/.plx/config.json"));
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.ClusterId))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "cluster id missing",
                "pass the cluster id as the positional argument"));
            return 3;
        }

        if (!settings.Yes)
        {
            var prompt = settings.Purge
                ? $"delete cluster {settings.ClusterId} on {config.Host} AND purge its nodes?"
                : $"delete cluster {settings.ClusterId} on {config.Host}?";
            var confirm = AnsiConsole.Console.Prompt(
                new ConfirmationPrompt(MarkupExtensions.Warn(prompt) + " [y/N]"));
            if (!confirm)
            {
                AnsiConsole.Console.MarkupLine(ErrorFormatter.Info("cancelled", "no changes made"));
                return 0;
            }
        }

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            await client.DeleteClusterAsync(settings.ClusterId, default);
            AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
                $"{BannerArt.Icon.Ok} deleted {settings.ClusterId}"));
            if (settings.Purge)
            {
                AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
                    "  purge requested — v0.1 soft-deletes only; the host's Phase 5+ bulk-delete is the future path for hard-purge"));
            }

            return 0;
        }
        catch (ApiException ex) when ((int)ex.StatusCode == 403)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "missing permission",
                "the bearer token doesn't carry the clusters.delete permission; rotate the token or grant the claim"));
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