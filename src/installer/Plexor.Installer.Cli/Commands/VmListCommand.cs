// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmListCommand — `plx vm list`. Tabular listing of every workload
// in the resolved cluster. Columns:
//
//   VM ID    short workload id
//   NAME     operator-facing name
//   STATUS   color-coded lifecycle state (Provisioning / Running /
//            Stopped / Failed / Unknown)
//   NODE     assigned node id, or "pending scheduling" when null
//   CREATED  creation time (UTC, ISO-8601)
//
// Paged via the host's FilterQuery envelope (`?page=N&pageSize=N`).
// Empty cluster renders a single "no workloads" row so the
// operator doesn't see a blank table.
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
///     <c>plx vm list</c> — tabular listing of every workload in the
///     resolved cluster. Calls
///     <c>GET /api/v1/compute/clusters/{clusterId}/workloads</c> via
///     the Refit client.
/// </summary>
public sealed class VmListCommand : AsyncCommand<VmListSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, VmListSettings settings)
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
            var response = await client.ListWorkloadsAsync(
                config.Cluster,
                settings.Page,
                settings.PageSize,
                default);
            VmListRenderer.Render(config.Cluster, response);
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
///     File-static renderer for <see cref="VmListCommand" />.
///     Pulled out per code-shape.md §1a (no private methods on
///     production classes).
/// </summary>
file static class VmListRenderer
{
    /// <summary>
    ///     Render the paged workload list as a Plexor-styled
    ///     table. Each row carries: short workload id, name,
    ///     colour-coded status, assigned node (or
    ///     "pending scheduling"), creation timestamp.
    /// </summary>
    /// <param name="clusterId">Cluster being reported.</param>
    /// <param name="page">Paged response from the host.</param>
    public static void Render(string clusterId, HostWorkloadPage page)
    {
        var table = new TableBuilder()
            .WithTitle($"workloads in {clusterId}  (page {page.Page} of {PageCount(page)}, {page.Total} total)")
            .AddColumn("VM ID", align: ColumnAlign.Left)
            .AddColumn("NAME", align: ColumnAlign.Left)
            .AddColumn("STATUS", align: ColumnAlign.Center)
            .AddColumn("NODE", align: ColumnAlign.Left)
            .AddColumn("CREATED", align: ColumnAlign.Right)
            .Build();

        if (page.Items.Count == 0)
        {
            table.AddRow(
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("no workloads"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"));
        }
        else
        {
            foreach (var workload in page.Items)
            {
                table.AddRow(
                    MarkupExtensions.Muted(ShortId(workload.Id)),
                    MarkupExtensions.B(workload.Name),
                    Colorize(workload.State),
                    MarkupExtensions.Muted(workload.AssignedNodeId is null
                        ? $"{BannerArt.Icon.Pending} pending scheduling"
                        : ShortId(workload.AssignedNodeId)),
                    MarkupExtensions.Muted(FormatCreatedAt(workload.CreatedAt)));
            }
        }

        AnsiConsole.Write(table);
    }

    private static int PageCount(HostWorkloadPage page)
    {
        if (page.PageSize <= 0)
        {
            return 1;
        }

        var totalPages = (page.Total + page.PageSize - 1) / page.PageSize;
        return Math.Max(1, totalPages);
    }

    private static string ShortId(string id)
    {
        var underscore = id.IndexOf('_');
        return underscore is >= 0 && underscore + 9 < id.Length
            ? id[..(underscore + 9)] + "…"
            : id;
    }

    private static string Colorize(HostWorkloadState state)
    {
        return state switch
        {
            HostWorkloadState.Running => MarkupExtensions.Ok($"{BannerArt.Icon.Status} running"),
            HostWorkloadState.Provisioning => MarkupExtensions.Idl($"{BannerArt.Icon.Pending} provisioning"),
            HostWorkloadState.Stopped => MarkupExtensions.Muted($"{BannerArt.Icon.Pending} stopped"),
            HostWorkloadState.Failed => MarkupExtensions.Err($"{BannerArt.Icon.Error} failed"),
            HostWorkloadState.Unknown => MarkupExtensions.Warn($"{BannerArt.Icon.Pending} unknown"),
            _ => MarkupExtensions.Muted(state.ToString())
        };
    }

    private static string FormatCreatedAt(DateTimeOffset createdAt)
    {
        return createdAt.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
