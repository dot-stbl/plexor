// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmStopCommand — `plx vm stop <vm-id>`. Calls
// `POST /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}/actions/stop`
// on the resolved host. The `--force` flag is accepted for forward
// compatibility (v0.1 backend does not differentiate hard vs
// graceful stop); when passed, the CLI surfaces a one-line "[warn]
// --force was passed but is ignored" notice so the operator sees
// whether the request actually behaved as expected.
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
///     <c>plx vm stop &lt;vm-id&gt;</c> — gracefully shut down a
///     running workload. Calls
///     <c>POST .../workloads/{workloadId}/actions/stop</c> via the
///     Refit client.
/// </summary>
public sealed class VmStopCommand : AsyncCommand<VmStopSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, VmStopSettings settings)
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

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            var response = await client.StopWorkloadAsync(
                config.Cluster,
                settings.VmId,
                default);
            VmActionRenderer.Render("stopped", settings.VmId, response);
            if (settings.Force)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Console.MarkupLine(MarkupExtensions.Warn(
                    $"{BannerArt.Icon.Warn} --force was passed but is ignored by the v0.1 backend; the workload was stopped gracefully"));
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
}
