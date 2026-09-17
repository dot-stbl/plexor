// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterRotateTokenCommand — `plx cluster rotate-token
// <cluster-id>`. Calls
// `POST /api/v1/compute/clusters/{id}/rotate-join-token` on the
// host; prints the new join token wrapped in a
// "sensitive — shown once" warning banner + the new join URL +
// the previous-token-now-invalid note.
//
// Rotation is non-destructive to cluster state (joined nodes
// keep running on their existing node-bearer tokens). Only the
// join-token is revoked, so a NodeAgent mid-join that hasn't
// redeemed the token yet must paste the new one.
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
///     <c>plx cluster rotate-token &lt;cluster-id&gt;</c> — revoke
///     the cluster's current join token and mint a new one. Calls
///     <c>POST /api/v1/compute/clusters/{id}/rotate-join-token</c>
///     via the Refit client and surfaces the new token (sensitive;
///     shown once).
/// </summary>
public sealed class ClusterRotateTokenCommand : AsyncCommand<ClusterRotateTokenSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, ClusterRotateTokenSettings settings)
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
            var response = await client.RotateClusterJoinTokenAsync(settings.ClusterId, default);
            ClusterRotateTokenRenderer.Render(response);
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
///     File-static renderer for
///     <see cref="ClusterRotateTokenCommand" />. Pulled out per
///     code-shape.md §1a (no private methods on production
///     classes).
/// </summary>
file static class ClusterRotateTokenRenderer
{
    /// <summary>
    ///     Print the cluster id + the previous-token-invalidated
    ///     note + the new join endpoint + the new join token
    ///     wrapped in a banner warning + the token's expiry.
    /// </summary>
    /// <param name="response">Rotate-token response from the host.</param>
    public static void Render(HostJoinTokenResult response)
    {
        var expires = FormatTimestamp(response.ExpiresAt);
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok(
            $"{BannerArt.Icon.Status} join token rotated for {response.ClusterId}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Warn(
            $"  {BannerArt.Icon.Warn} previous token is now invalid — any NodeAgent mid-join must use the new one"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  endpoint:  {response.Endpoint}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  expires:   {expires}"));

        AnsiConsole.WriteLine();
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Warn(
            $"{BannerArt.Icon.Warn} sensitive — shown once"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.B("NEW JOIN TOKEN"));
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