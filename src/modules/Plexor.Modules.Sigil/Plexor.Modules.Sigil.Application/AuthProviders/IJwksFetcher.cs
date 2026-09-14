// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IJwksFetcher — fetches and caches the JWKS (JSON Web Key Set) document
// for a configured OIDC authority. Used by ExternalOidcAuthProvider
// (Phase 4.6.2b) to validate the signature on inbound ID-token JWTs.
//
// Lives in Plexor.Modules.Sigil.Application.AuthProviders alongside
// IAuthProvider so the OIDC provider depends on an abstraction; the
// concrete IMemoryCache-backed implementation lives in
// Plexor.Modules.Sigil.Infrastructure.AuthProviders.JwksFetcher.
// ============================================================================

using Microsoft.IdentityModel.Tokens;

namespace Plexor.Modules.Sigil.Application.AuthProviders;

/// <summary>
///     Resolves the <see cref="JsonWebKeySet" /> for an external OIDC
///     authority. The fetcher owns its own TTL cache so repeated
///     inbound JWTs against the same authority don't re-fetch the
///     JWKS document on every request.
/// </summary>
/// <remarks>
///     <para><b>Cache key.</b> Authority URL (after trailing-slash
///     normalisation). Two different OIDC authorities — e.g.
///     <c>https://kc.example.com/realms/alpha</c> vs
///     <c>https://kc.example.com/realms/beta</c> — never collide on
///     the same cache entry.</para>
///     <para><b>TTL.</b> 1 hour. IDP signing keys rotate slowly (most
///     operators rotate once a quarter); the kid-miss fallback in
///     <c>ExternalOidcAuthProvider</c> covers the case where Plexor's
///     cache is stale relative to the IDP's current signing key set.
///     The cache is in-memory only — a process restart triggers a
///     cold fetch on the first request, which is the desired
///     behaviour for operators that rotate the host process during a
///     key rotation.</para>
///     <para><b>HTTP path.</b> Discovery document at
///     <c>{authority}/.well-known/openid-configuration</c> → reads
///     <c>jwks_uri</c> → fetches the JWKS. Uses the named
///     <c>Plexor-OidcDiscovery</c> <see cref="System.Net.Http.IHttpClientFactory" />
///     client (10s timeout, no auth, no retries — registered in
///     Plexor.Host/Program.cs as part of Phase 4.6.1).</para>
/// </remarks>
public interface IJwksFetcher
{
    /// <summary>
    ///     Resolve the JWKS for the given authority. Returns the
    ///     cached set on warm cache; fetches + caches on cold cache.
    ///     Throws <see cref="InvalidOperationException" /> when the
    ///     discovery document or JWKS fetch fails — the caller
    ///     treats this as "this provider doesn't claim this
    ///     credential" (returns <c>null</c> from
    ///     <see cref="IAuthProvider.ResolveAsync" />).
    /// </summary>
    /// <param name="authority">
    ///     The OIDC issuer URL. <c>https://kc.plexor.example.com/realms/plexor</c>.
    ///     Trailing slashes are tolerated.
    /// </param>
    /// <param name="cancellationToken">Forwarded to the HTTP fetch.</param>
    /// <returns>The cached or freshly-fetched JWKS.</returns>
    public Task<JsonWebKeySet> GetKeySetAsync(
        string authority,
        CancellationToken cancellationToken = default);
}
