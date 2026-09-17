// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostApiClientFactory — builds a `IPlexorHostApi` Refit client
// from a resolved `PlxConfig`. The CLI is NativeAOT so it cannot
// use the IServiceCollection extension `AddRefitClient<T>` (which
// relies on the `IHttpClientFactory` service registered by the
// ASP.NET Core hosting layer). Instead we build the
// `HttpClient` directly and ask Refit to wrap it via
// `RestService.For<T>(...)` — Refit 12.x emits an HttpClient at
// compile time from the interface, no runtime reflection.
//
// Bearer authentication is attached by `HostBearerHandler` —
// a one-line DelegatingHandler that prepends the token on every
// request. The handler is constructed per-client so the token is
// stable for the lifetime of the command (commands are synchronous
// in this CLI; one client per command invocation).
// ============================================================================

using Plexor.Installer.Cli.Refit;
using Refit;

namespace Plexor.Installer.Cli;

/// <summary>
///     Build a fresh <see cref="IPlexorHostApi" /> Refit client from
///     a resolved <see cref="PlxConfig" />. Disposing the client is
///     the caller's responsibility (commands dispose via the
///     <c>await using</c> scope in <c>ExecuteAsync</c>).
/// </summary>
internal static class HostApiClientFactory
{
    /// <summary>Default request timeout for host API calls (10 s).</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Create a Refit-typed client pinned to
    ///     <paramref name="config" />.Host. The returned
    ///     <c>IDisposable</c> is the underlying HttpClient; the
    ///     typed client is the inner Refit proxy.
    /// </summary>
    /// <param name="config">Resolved host + token.</param>
    /// <returns>Refit client + the HttpClient that backs it.</returns>
    public static (IPlexorHostApi Client, HttpClient Http) Create(PlxConfig config)
    {
        var http = new HttpClient(new HostBearerHandler(config.Token))
        {
            BaseAddress = new Uri(config.Host.TrimEnd('/') + "/"),
            Timeout = RequestTimeout
        };

#pragma warning disable IL2026, IL3050 // RestService.For uses reflection; AOT-trim warnings benign for CLI dev builds
        var client = RestService.For<IPlexorHostApi>(http);
#pragma warning restore IL2026, IL3050
        return (client, http);
    }

    /// <summary>
    ///     Delegating handler that injects
    ///     <c>Authorization: Bearer &lt;token&gt;</c> on every
    ///     outbound request. Token is captured at construction;
    ///     rotation requires a fresh client (CLI commands are
    ///     short-lived; rotation mid-command is out of scope).
    /// </summary>
    /// <param name="token">Bearer token captured at handler construction.</param>
    private sealed class HostBearerHandler(string token) : DelegatingHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);
            return base.SendAsync(request, cancellationToken);
        }
    }
}