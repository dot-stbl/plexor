// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmDeleteCommand — `plx vm delete <vm-id>`. Confirms before the
// destructive op (unless `--yes`), calls
// `DELETE /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}`
// on the resolved host. The `--keep-storage` flag is accepted for
// forward compatibility (v0.1 backend always removes the runtime
// handle AND the backing storage); when passed, the CLI surfaces a
// one-line "[warn] --keep-storage was passed but is ignored" notice.
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
///     <c>plx vm delete &lt;vm-id&gt;</c> — soft-delete a workload.
///     Calls <c>DELETE .../workloads/{workloadId}</c> via the Refit
///     client. Destructive — confirms by default; pass <c>--yes</c>
///     to skip the prompt.
/// </summary>
public sealed class VmDeleteCommand : AsyncCommand<VmDeleteSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, VmDeleteSettings settings)
    {
        var config = settings.ResolveConfig();
        if (config is null)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "host / token missing",
                "pass --host and --token, or set PLX_HOST + PLX_TOKEN, or write ~/.plx/config.json"));
            return 2;
        }

        if (string.IsNullOrWhiteSpace(config.Cluster))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "cluster missing",
                "pass --cluster <ID>, or set PLX_CLUSTER, or add cluster to ~/.plx/config.json"));
            return 3;
        }

        if (string.IsNullOrWhiteSpace(settings.VmId))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "vm id missing",
                "pass the vm id as the positional argument"));
            return 3;
        }

        if (!settings.Yes)
        {
            var confirm = AnsiConsole.Console.Prompt(
                new ConfirmationPrompt(
                    MarkupExtensions.Warn(
                        $"delete workload {settings.VmId} on {config.Host}?")
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
            await client.DeleteWorkloadAsync(
                config.Cluster,
                settings.VmId,
                default);
            AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
                $"{BannerArt.Icon.Ok} deleted {ShortId(settings.VmId)}"));

            if (settings.KeepStorage)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Console.MarkupLine(MarkupExtensions.Warn(
                    $"{BannerArt.Icon.Warn} --keep-storage was passed but is ignored by the v0.1 backend; the workload's backing storage was also removed"));
            }

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

    private static string ShortId(string id)
    {
        var underscore = id.IndexOf('_');
        return underscore is >= 0 && underscore + 9 < id.Length
            ? id[..(underscore + 9)] + "…"
            : id;
    }
}
