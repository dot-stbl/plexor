// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ApiCallRunner — runs a Refit call and translates
// <see cref="Refit.ApiException" /> into the transport's
// <see cref="HttpRequestException" /> contract. Extracted from
// HttpCommandTransport (Sep 2026) per §1a (no private methods on
// production classes).
// ============================================================================

using System.Globalization;
using Refit;

namespace Plexor.NodeAgent.Infrastructure;

/// <summary>
///     Helper that runs a Refit call and converts a non-2xx
///     <see cref="ApiException" /> into an <see cref="HttpRequestException" />.
///     The worker loop catches the latter and decides what to do
///     (skip the cycle, restart, etc.).
/// </summary>
internal static class ApiCallRunner
{
    /// <summary>
    ///     Run <paramref name="call" />, log non-2xx responses as
    ///     warnings, and rethrow the underlying <see cref="ApiException" />
    ///     wrapped in an <see cref="HttpRequestException" />.
    /// </summary>
    /// <typeparam name="T">Return type of the Refit call.</typeparam>
    /// <param name="call">The Refit call to run.</param>
    /// <param name="operation">One of <c>register</c> / <c>heartbeat</c> / <c>poll</c> / <c>submit</c> — used in the log line.</param>
    /// <param name="logger">Structured logger.</param>
    /// <exception cref="HttpRequestException">Always (on non-2xx) wrapping the original <see cref="ApiException" />.</exception>
    public static async Task<T> RunAsync<T>(
        Func<Task<T>> call,
        string operation,
        ILogger logger)
    {
        try
        {
            return await call();
        }
        catch (ApiException ex)
        {
            logger.LogWarning(
                "Control plane {Operation} returned {Status} ({StatusCode}): {Content}",
                operation,
                (int)ex.StatusCode,
                ex.StatusCode,
                ex.HasContent ? "(body available — see Refit diagnostics)" : "(no body)");

            throw new HttpRequestException(
                $"Control plane {operation} returned " +
                string.Create(CultureInfo.InvariantCulture, $"{(int)ex.StatusCode} {ex.StatusCode}."),
                ex,
                ex.StatusCode);
        }
    }
}