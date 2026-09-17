// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterListCommand — `plx cluster list`. Tabular listing of
// every cluster in the caller's org. Columns: ClusterId, Name,
// Status, NodeCount, CreatedAt.
//
// Empty org renders a single "no clusters yet" row so the
// operator doesn't see a blank table. Pagination follows the
// host's standard FilterQuery envelope (`?page=` + `?pageSize=`);
// the host's handler clamps to [1, 100].
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
///     <c>plx cluster list</c> — tabular listing of every cluster
///     in the caller's org. Calls <c>GET /api/v1/compute/clusters</c>
///     via the Refit client with the configured page / page-size
///     flags (defaults <c>?page=1&amp;pageSize=25</c>).
/// </summary>
public sealed class ClusterListCommand : AsyncCommand<ClusterListSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ClusterListSettings settings)
    {
        var config = settings.ResolveConfig();
        if (config is null)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "host / token missing",
                "pass --host and --token, or set PLX_HOST + PLX_TOKEN, or write ~/.plx/config.json"));
            return 2;
        }

        if (settings.Page < 1)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "invalid --page",
                "page must be >= 1"));
            return 3;
        }

        if (settings.PageSize < 1)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "invalid --page-size",
                "page-size must be >= 1"));
            return 3;
        }

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            var page = await client.ListClustersAsync(settings.Page, settings.PageSize, default);
            ClusterListRenderer.Render(page);
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
///     File-static renderer for <see cref="ClusterListCommand" />.
///     Pulled out per code-shape.md §1a (no private methods on
///     production classes).
/// </summary>
file static class ClusterListRenderer
{
    /// <summary>
    ///     Render the cluster page as a Plexor-styled table. Each row
    ///     carries: short cluster id, name, status (color-coded),
    ///     node-count summary, creation timestamp. Empty pages
    ///     render a single "no clusters yet" row.
    /// </summary>
    /// <param name="page">Page response from the host.</param>
    public static void Render(HostClusterPage page)
    {
        var pageCount = Math.Max(1, (page.Total + page.PageSize - 1) / page.PageSize);
        var table = new TableBuilder()
            .WithTitle($"clusters (page {page.Page} of {pageCount}, {page.Total} total)")
            .AddColumn("CLUSTER ID", align: ColumnAlign.Left)
            .AddColumn("NAME", align: ColumnAlign.Left)
            .AddColumn("STATUS", align: ColumnAlign.Center)
            .AddColumn("NODES", align: ColumnAlign.Right)
            .AddColumn("CREATED", align: ColumnAlign.Right)
            .Build();

        if (page.Items.Count == 0)
        {
            table.AddRow(
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("no clusters yet"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"));
        }
        else
        {
            foreach (var cluster in page.Items)
            {
                table.AddRow(
                    MarkupExtensions.Muted(ShortId(cluster.Id)),
                    MarkupExtensions.B(cluster.Name),
                    ColorizeStatus(cluster.Status),
                    MarkupExtensions.Muted(FormatNodeCounts(cluster.NodeCounts)),
                    MarkupExtensions.Muted(FormatTimestamp(cluster.CreatedAt)));
            }
        }

        AnsiConsole.Write(table);
    }

    private static string ShortId(string id)
    {
        var underscore = id.IndexOf('_');
        return underscore is >= 0 && underscore + 9 < id.Length
            ? id[..(underscore + 9)] + "…"
            : id;
    }

    private static string ColorizeStatus(HostClusterStatus status)
    {
        return status switch
        {
            HostClusterStatus.Ready => MarkupExtensions.Ok($"{BannerArt.Icon.Status} ready"),
            HostClusterStatus.Provisioning => MarkupExtensions.Idl($"{BannerArt.Icon.Running} provisioning"),
            HostClusterStatus.Pending => MarkupExtensions.Idl($"{BannerArt.Icon.Pending} pending"),
            HostClusterStatus.Degraded => MarkupExtensions.Warn($"{BannerArt.Icon.Warn} degraded"),
            HostClusterStatus.Offline => MarkupExtensions.Err($"{BannerArt.Icon.Error} offline"),
            _ => MarkupExtensions.Muted(status.ToString())
        };
    }

    private static string FormatNodeCounts(HostNodeCounts counts)
    {
        if (counts.Total == 0)
        {
            return "0";
        }

        return $"{counts.Total} ({counts.Ready} ready · {counts.Pending} pending · {counts.Draining} draining · {counts.Offline} offline)";
    }

    private static string FormatTimestamp(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}