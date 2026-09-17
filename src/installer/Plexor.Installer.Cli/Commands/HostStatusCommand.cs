// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostStatusCommand — `plx host status`. Calls
// `GET /api/v1/nodes?clusterId=...` on the resolved host and
// renders a single summary table:
//
//     CLUSTER             status
//     cluster_eu_1       ✓ healthy  (3 ready · 0 degraded · 0 offline)
//
// For now the command is cluster-scoped (v0.1 limitation: the host
// has no cross-cluster list endpoint yet). The --all flag is
// accepted but ignored, matching HostStatusSettings.
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
///     <c>plx host status</c> — overall health check for one
///     cluster on the target Plexor.Host. Calls
///     <c>GET /api/v1/nodes</c> via the Refit client and renders
///     a single-row summary table with the ready / degraded /
///     offline counts.
/// </summary>
public sealed class HostStatusCommand : AsyncCommand<HostStatusSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, HostStatusSettings settings)
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

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            var response = await client.ListNodesAsync(config.Cluster, default);
            HostStatusRenderer.Render(config.Cluster, response.Nodes);
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

/// <summary>
///     File-static renderer for <see cref="HostStatusCommand" />.
///     Pulled out per code-shape.md §1a (no private methods on
///     production classes).
/// </summary>
file static class HostStatusRenderer
{
    /// <summary>
    ///     Render the cluster + per-status counts as a single
    ///     summary line. <paramref name="nodes" /> may be empty
    ///     when the cluster has no nodes yet — the renderer shows
    ///     a pending marker instead of an error.
    /// </summary>
    /// <param name="clusterId">Cluster being reported.</param>
    /// <param name="nodes">Node list response from the host.</param>
    public static void Render(string clusterId, IReadOnlyList<HostNodeResponse> nodes)
    {
        var (ready, pending, draining, gone) = Aggregate(nodes);
        var total = nodes.Count;
        var status = ResolveStatus(nodes.Count, gone, draining);

        var table = new TableBuilder()
            .WithTitle("host status")
            .AddColumn("CLUSTER", align: ColumnAlign.Left)
            .AddColumn("STATUS", align: ColumnAlign.Left)
            .AddColumn("BREAKDOWN", align: ColumnAlign.Left)
            .Build();
        table.AddRow(
            MarkupExtensions.B(clusterId),
            status,
            MarkupExtensions.Muted($"{ready} ready · {pending} pending · {draining} draining · {gone} gone · {total} total"));
        AnsiConsole.Write(table);
    }

    private static (int Ready, int Pending, int Draining, int Gone) Aggregate(
        IReadOnlyList<HostNodeResponse> nodes)
    {
        var ready = 0;
        var pending = 0;
        var draining = 0;
        var gone = 0;
        foreach (var node in nodes)
        {
            switch (node.Status)
            {
                case HostNodeStatus.Ready: ready++; break;
                case HostNodeStatus.Pending: pending++; break;
                case HostNodeStatus.Draining: draining++; break;
                case HostNodeStatus.Gone: gone++; break;
            }
        }

        return (ready, pending, draining, gone);
    }

    private static string ResolveStatus(int nodeCount, int gone, int draining)
    {
        if (nodeCount == 0)
        {
            return MarkupExtensions.Idl($"{BannerArt.Icon.Pending} 0 nodes");
        }

        if (gone > 0)
        {
            return MarkupExtensions.Err($"{BannerArt.Icon.Status} degraded ({gone} gone)");
        }

        if (draining > 0)
        {
            return MarkupExtensions.Warn($"{BannerArt.Icon.Status} degraded ({draining} draining)");
        }

        return MarkupExtensions.Ok($"{BannerArt.Icon.Status} healthy");
    }
}