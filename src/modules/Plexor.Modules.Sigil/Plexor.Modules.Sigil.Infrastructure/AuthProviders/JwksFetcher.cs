// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JwksFetcher — IJwksFetcher implementation. Fetches the OIDC
// discovery document + JWKS via the named "Plexor-OidcDiscovery"
// IHttpClientFactory client; caches the JWKS in IMemoryCache keyed by
// normalised authority URL with a 1-hour TTL.
//
// v0.1 trade-offs:
//   - In-memory cache only. A multi-pod host has a per-pod cache;
//     cold fetch on first request after a process restart is fine.
//   - No proactive refresh on kid miss. The OIDC provider catches a
//     kid miss on its own and falls through to AuthResolution = null;
//     the dispatcher (4.6.2c) decides what to do with the failure.
//   - No size cap on the cache. Each entry is one JWKS document;
//     ~10s of kilobytes per authority; bounded by the number of
//     distinct authorities (= number of OIDC-configured tenants),
//     which is small in v0.1.
// ============================================================================

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders;

/// <summary>
///     <see cref="Application.AuthProviders.IJwksFetcher" />
///     implementation backed by <see cref="IHttpClientFactory" /> +
///     <see cref="IMemoryCache" />.
/// </summary>
/// <remarks>
///     <para><b>Named client.</b> Resolves
///     <c>Plexor-OidcDiscovery</c> via <see cref="IHttpClientFactory" />.
///     The client is registered in <c>Plexor.Host/Program.cs</c> with
///     a 10s timeout and a Plexor User-Agent; no auth, no retries —
///     a transient failure should bubble up to the OIDC provider as
///     "this provider can't authoritatively verify the credential
///     right now" rather than retrying against an authority that
///     may be having a real outage.</para>
///     <para><b>Singleton lifetime.</b> The fetcher is stateless beyond
///     the cache (which is itself a singleton via
///     <c>AddMemoryCache</c>). One instance per host process.</para>
/// </remarks>
/// <param name="httpClientFactory">
///     ASP.NET Core HTTP client factory. The fetcher always names the
///     discovery client explicitly so no future typed-client
///     registration can poison the JWKS path.
/// </param>
/// <param name="cache">
///     Process-local memory cache. TTL = 1h; entries are JWKS
///     documents keyed by normalised authority URL.
/// </param>
/// <param name="logger">Structured logger.</param>
public sealed class JwksFetcher(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<JwksFetcher> logger) : Application.AuthProviders.IJwksFetcher
{
    private const string HttpClientName = "Plexor-OidcDiscovery";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private const string CacheKeyPrefix = "plexor.jwks.";

    /// <inheritdoc />
    public async Task<JsonWebKeySet> GetKeySetAsync(
        string authority,
        CancellationToken cancellationToken = default)
    {
        // Trailing-slash normalisation collapses
        // `https://kc.example.com/realms/plexor/` and
        // `https://kc.example.com/realms/plexor` to the same cache
        // key. Inlined here because a separate helper would be a
        // single-call `private static` — banned by
        // `code-shape.md §9`.
        var normalised = authority.TrimEnd('/');
        var cacheKey = CacheKeyPrefix + normalised;

        if (cache.TryGetValue(cacheKey, out JsonWebKeySet? cached) && cached is not null)
        {
            return cached;
        }

        var http = httpClientFactory.CreateClient(HttpClientName);
        var discoveryUrl = $"{normalised}/.well-known/openid-configuration";
        var discovery = await http.GetFromJsonAsync<OidcDiscoveryDocument>(
            discoveryUrl,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"OIDC discovery at '{discoveryUrl}' returned null.");

        if (string.IsNullOrWhiteSpace(discovery.JwksUri))
        {
            throw new InvalidOperationException(
                $"OIDC discovery at '{discoveryUrl}' did not advertise a jwks_uri.");
        }

        var keySet = await http.GetFromJsonAsync<JsonWebKeySet>(
            discovery.JwksUri,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"JWKS at '{discovery.JwksUri}' returned null.");

        cache.Set(cacheKey, keySet, CacheLifetime);
        logger.LogInformation(
            "JwksFetcher: cached {KeyCount} key(s) for {Authority}",
            keySet.Keys.Count,
            normalised);
        return keySet;
    }

    /// <summary>
    ///     Minimal projection of the OIDC discovery document (RFC 8414 /
    ///     OpenID Connect Discovery 1.0 §4). Plexor only needs
    ///     <c>jwks_uri</c> today; other fields are present so future
    ///     phases (token-introspection in 4.6.3, JWKS rotation in
    ///     Phase 5+) can read them without a model change.
    /// </summary>
    /// <param name="Issuer"></param>
    /// <param name="JwksUri"></param>
    /// <param name="AuthorizationEndpoint"></param>
    /// <param name="TokenEndpoint"></param>
    private sealed record OidcDiscoveryDocument(
        [property: JsonPropertyName("issuer")] string? Issuer,
        [property: JsonPropertyName("jwks_uri")] string? JwksUri,
        [property: JsonPropertyName("authorization_endpoint")] string? AuthorizationEndpoint,
        [property: JsonPropertyName("token_endpoint")] string? TokenEndpoint);
}
