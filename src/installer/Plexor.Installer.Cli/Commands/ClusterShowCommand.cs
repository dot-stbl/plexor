// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterShowCommand — `plx cluster show <cluster-id>`. Reads
// the single-cluster detail endpoint with embedded child nodes
// and renders a Plexor-styled panel + a node-list table.
// Read-only; no confirmation prompt.
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
///     <c>plx cluster show &lt;cluster-id&gt;</c> — single-cluster
///     detail with embedded child nodes. Calls
///     <c>GET /api/v1/compute/clusters/{id}</c> via the Refit
///     client and renders a Plexor-styled panel + a node-list
///     table.
/// </summary>
public sealed class ClusterShowCommand : AsyncCommand<ClusterShowSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ClusterShowSettings settings)
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

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            var cluster = await client.GetClusterAsync(settings.ClusterId, default);
            ClusterShowRenderer.Render(cluster);
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
///     File-static renderer for <see cref="ClusterShowCommand" />.
///     Pulled out per code-shape.md §1a (no private methods on
///     production classes).
/// </summary>
file static class ClusterShowRenderer
{
    /// <summary>
    ///     Render the cluster detail as a Plexor-styled panel
    ///     (id + name + region + status + endpoint + runtime +
    ///     version + wireguard + token-expiry) followed by a
    ///     node-list table (id + hostname + role + status). Empty
    ///     node lists render a single "no nodes joined yet" row.
    /// </summary>
    /// <param name="cluster">Single-cluster detail response.</param>
    public static void Render(HostClusterDetail cluster)
    {
        var lines = new List<string>
        {
            $"{MarkupExtensions.Muted("id:        ")}{cluster.Id}",
            $"{MarkupExtensions.Muted("name:      ")}{MarkupExtensions.B(cluster.Name)}",
            $"{MarkupExtensions.Muted("region:    ")}{cluster.Region}",
            $"{MarkupExtensions.Muted("status:    ")}{ColorizeStatus(cluster.Status)}",
            $"{MarkupExtensions.Muted("endpoint:  ")}{cluster.Endpoint}",
            $"{MarkupExtensions.Muted("runtime:   ")}{cluster.RuntimeId}",
            $"{MarkupExtensions.Muted("host ver:  ")}{cluster.HostVersion}",
            $"{MarkupExtensions.Muted("wireguard: ")}{Truncate(cluster.WireguardPublicKey, 24)}",
            $"{MarkupExtensions.Muted("providers: ")}{(cluster.InstallProviders.Count == 0 ? "—" : string.Join(", ", cluster.InstallProviders))}",
            $"{MarkupExtensions.Muted("token exp: ")}{(cluster.JoinTokenExpiresAt is null ? "—" : FormatTimestamp(cluster.JoinTokenExpiresAt.Value))}",
            $"{MarkupExtensions.Muted("created:   ")}{FormatTimestamp(cluster.CreatedAt)}",
            $"{MarkupExtensions.Muted("updated:   ")}{FormatTimestamp(cluster.UpdatedAt)}",
        };

        AnsiConsole.Write(new Panel(string.Join("\n", lines))
            .Header($" cluster {cluster.Id} ")
            .BorderColor(ColorPalette.Muted)
            .Expand());

        AnsiConsole.WriteLine();

        var table = new TableBuilder()
            .WithTitle($"nodes in {cluster.Name}")
            .AddColumn("NODE ID", align: ColumnAlign.Left)
            .AddColumn("HOSTNAME", align: ColumnAlign.Left)
            .AddColumn("ROLE", align: ColumnAlign.Center)
            .AddColumn("STATUS", align: ColumnAlign.Center)
            .Build();

        if (cluster.Nodes.Count == 0)
        {
            table.AddRow(
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("no nodes joined yet"),
                MarkupExtensions.Muted("—"),
                MarkupExtensions.Muted("—"));
        }
        else
        {
            foreach (var node in cluster.Nodes)
            {
                table.AddRow(
                    MarkupExtensions.Muted(ShortId(node.Id)),
                    MarkupExtensions.B(node.Hostname),
                    MarkupExtensions.Muted(node.Role.ToString().ToLowerInvariant()),
                    ColorizeNodeStatus(node.Status));
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

    private static string ColorizeNodeStatus(int status)
    {
        return status switch
        {
            1 => MarkupExtensions.Ok($"{BannerArt.Icon.Status} ready"),
            0 => MarkupExtensions.Idl($"{BannerArt.Icon.Pending} pending"),
            2 => MarkupExtensions.Warn($"{BannerArt.Icon.Status} draining"),
            3 => MarkupExtensions.Err($"{BannerArt.Icon.Status} gone"),
            _ => MarkupExtensions.Muted($"status={status}")
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    private static string FormatTimestamp(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm:ss 'UTC'",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}