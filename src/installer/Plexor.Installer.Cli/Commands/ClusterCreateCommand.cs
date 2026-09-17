// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterCreateCommand — `plx cluster create --name <name>`.
// Provisions a new cluster and prints the join token wrapped in
// a "sensitive — shown once" warning banner, plus the join URL
// the operator pastes into the NodeAgent's install.sh.
//
// The token printout is wrapped in a clear "sensitive — shown
// once" warning so it doesn't get lost in the scrollback. The
// token's `ExpiresAt` is surfaced so operators know how long
// the window stays open.
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
///     <c>plx cluster create --name &lt;name&gt;</c> — provision a
///     new cluster. Calls <c>POST /api/v1/compute/clusters</c> via
///     the Refit client and surfaces the returned join token
///     (sensitive; shown once) plus the join endpoint.
/// </summary>
public sealed class ClusterCreateCommand : AsyncCommand<ClusterCreateSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ClusterCreateSettings settings)
    {
        var config = settings.ResolveConfig();
        if (config is null)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "host / token missing",
                "pass --host and --token, or set PLX_HOST + PLX_TOKEN, or write ~/.plx/config.json"));
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "cluster name missing",
                "pass --name <NAME>"));
            return 3;
        }

        var role = ParseInitialRole(settings.InitialNodeRole);
        var region = settings.Region ?? string.Empty;

        var (client, http) = HostApiClientFactory.Create(config);
        try
        {
            var request = new HostCreateClusterRequest(settings.Name, region, role);
            var response = await client.CreateClusterAsync(request, default);
            ClusterCreateRenderer.Render(response);
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

    private static HostNodeRole ParseInitialRole(string? raw)
    {
        return raw?.ToLowerInvariant() switch
        {
            "control" => HostNodeRole.Control,
            null or "" or "compute" or "worker" => HostNodeRole.Compute,
            _ => HostNodeRole.Compute
        };
    }
}

/// <summary>
///     File-static renderer for <see cref="ClusterCreateCommand" />.
///     Pulled out per code-shape.md §1a (no private methods on
///     production classes).
/// </summary>
file static class ClusterCreateRenderer
{
    /// <summary>
    ///     Print the new cluster id + name + the join token wrapped
    ///     in a banner warning + the join endpoint + the token's
    ///     expiry. The token is the only thing the CLI surfaces in
    ///     cleartext; the operator must paste it into the
    ///     NodeAgent's install.sh before it expires.
    /// </summary>
    /// <param name="response">Create response from the host.</param>
    public static void Render(HostJoinTokenResult response)
    {
        var expires = FormatTimestamp(response.ExpiresAt);
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
            $"{BannerArt.Icon.Status} cluster created"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  id:        {response.ClusterId}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  endpoint:  {response.Endpoint}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  expires:   {expires}"));

        AnsiConsole.WriteLine();
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Warn(
            $"{BannerArt.Icon.Warn} sensitive — shown once"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.B("JOIN TOKEN"));
        AnsiConsole.Console.WriteLine(response.Token);
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  paste into the NodeAgent's install.sh as --join-token; expires {expires}"));
    }

    private static string FormatTimestamp(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(
            "yyyy-MM-dd HH:mm:ss 'UTC'",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}