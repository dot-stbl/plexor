// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HealthProbe — GET http://host:port/health for the installer.
// Used by `plx upgrade` to verify the swapped binary is healthy
// before declaring victory. On failure the caller rolls the binary
// back to the .prev file and restarts the service.
// ============================================================================

using Plexor.Shared.Console;
using Spectre.Console;

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     Health probe for the Plexor.Host service. Returns 0 if the
///     endpoint returned a 2xx response, 6 for non-2xx, 7 for
///     connection failures (so the upgrade orchestrator can branch
///     on the cause).
/// </summary>
public static class HealthProbe
{
    /// <summary>2xx response.</summary>
    public const int ExitOk = 0;

    /// <summary>Endpoint returned non-2xx.</summary>
    public const int ExitBadStatus = 6;

    /// <summary>Endpoint unreachable / timed out.</summary>
    public const int ExitUnreachable = 7;

    /// <summary>
    ///     GET <paramref name="endpoint" /> with a short timeout
    ///     and return one of <see cref="ExitOk" />,
    ///     <see cref="ExitBadStatus" />, or
    ///     <see cref="ExitUnreachable" />. Connection errors are
    ///     logged as a warning (not an error — the caller decides).
    /// </summary>
    /// <param name="endpoint">Full URL of the health endpoint.</param>
    public static async Task<int> CheckAsync(string endpoint)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var response = await client.GetAsync(endpoint);
            return (int)response.StatusCode is >= 200 and < 300 ? ExitOk : ExitBadStatus;
        }
        catch (Exception ex)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Warn("health check failed", ex.Message));
            return ExitUnreachable;
        }
    }
}
