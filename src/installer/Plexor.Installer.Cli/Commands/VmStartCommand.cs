// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmStartCommand — `plx vm start <vm-id>`. Calls
// `POST /api/v1/compute/clusters/{clusterId}/workloads/{workloadId}/actions/start`
// on the resolved host and prints the new state the agent reported
// back. Long-polls the agent's acknowledgement (capped at 30 s on
// the host side; the CLI's 10 s HTTP timeout may surface a 504 if
// the agent is slow to reply).
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
///     <c>plx vm start &lt;vm-id&gt;</c> — start a previously
///     provisioned workload. Calls
///     <c>POST .../workloads/{workloadId}/actions/start</c> via the
///     Refit client.
/// </summary>
public sealed class VmStartCommand : AsyncCommand<VmStartSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, VmStartSettings settings)
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
            var response = await client.StartWorkloadAsync(
                config.Cluster,
                settings.VmId,
                default);
            VmActionRenderer.Render("started", settings.VmId, response);
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
