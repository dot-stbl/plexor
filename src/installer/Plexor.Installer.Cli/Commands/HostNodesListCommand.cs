// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodesListCommand — `plx host nodes list`. Tabular listing
// of every node in the resolved cluster. Columns:
// NodeId, Hostname, IP, Role, Status (color-coded), LastHeartbeat.
//
// Empty cluster renders a single "no nodes registered" row so the
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
///     <c>plx host nodes list</c> — tabular listing of every node
///     in the resolved cluster. Calls
///     <c>GET /api/v1/nodes?clusterId=...</c> via the Refit client.
/// </summary>
public sealed class HostNodesListCommand : AsyncCommand<HostNodesListSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, HostNodesListSettings settings)
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
            HostNodesListRenderer.Render(config.Cluster, response.Nodes);
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
///     File-static renderer for <see cref="HostNodesListCommand" />.
///     Pulled out per code-shape.md §1a (no private methods on
///     production classes).
/// </summary>
file static class HostNodesListRenderer
{
    /// <summary>
    ///     Render the node list as a Plexor-styled table. Each row
    ///     carries: short node id, hostname, IP, role, status
    ///     (color-coded), last heartbeat.
    /// </summary>
    /// <param name="clusterId">Cluster being reported.</param>
    /// <param name="nodes">Node list response from the host.</param>
    public static void Render(string clusterId, IReadOnlyList<HostNodeResponse> nodes)
    {
        var table = new TableBuilder()
            .WithTitle($"nodes in {clusterId}")
            .AddColumn("NODE ID", align: ColumnAlign.Left)
            .AddColumn("HOSTNAME", align: ColumnAlign.Left)
            .AddColumn("IP", align: ColumnAlign.Left)
            .AddColumn("ROLE", align: ColumnAlign.Center)
            .AddColumn("STATUS", align: ColumnAlign.Center)
            .AddColumn("LAST HEARTBEAT", align: ColumnAlign.Right)
            .Build();

        if (nodes.Count == 0)
        {
            table.AddRow(
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("no nodes registered"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"));
        }
        else
        {
            foreach (var node in nodes)
            {
                table.AddRow(
                    MarkupExtensions.Muted(ShortId(node.Id)),
                    MarkupExtensions.B(node.Hostname),
                    MarkupExtensions.Muted(node.IpAddress),
                    MarkupExtensions.Muted(node.Role.ToString().ToLowerInvariant()),
                    Colorize(node.Status),
                    MarkupExtensions.Muted(FormatLastHeartbeat(node.LastHeartbeatAt)));
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

    private static string Colorize(HostNodeStatus status)
    {
        return status switch
        {
            HostNodeStatus.Ready => MarkupExtensions.Ok($"{BannerArt.Icon.Status} ready"),
            HostNodeStatus.Pending => MarkupExtensions.Idl($"{BannerArt.Icon.Pending} pending"),
            HostNodeStatus.Draining => MarkupExtensions.Warn($"{BannerArt.Icon.Status} draining"),
            HostNodeStatus.Gone => MarkupExtensions.Err($"{BannerArt.Icon.Status} gone"),
            _ => MarkupExtensions.Muted(status.ToString())
        };
    }

    private static string FormatLastHeartbeat(DateTimeOffset? last)
    {
        return last is null
            ? "never"
            : last.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }
}