// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthProviderResolver — IAuthProviderResolver implementation. Routes a
// raw bearer credential to the right IAuthProvider based on the JWT
// `iss` claim, with an in-memory cache to avoid an OrgAuthProviderConfig
// DB roundtrip on every request.
//
// Resolution algorithm:
//
//   1. Empty / whitespace credential → return null.
//   2. Peek the JWT `iss` claim (no signature check). Failure → null.
//   3. Cache lookup: (iss → AuthProviderId). Hit → dispatch.
//   4. Cache miss:
//        - iss == "plexor" → Sigil (no DB read — the Sigil provider
//          self-routes by the `tid` claim in the JWT).
//        - iss != "plexor" → query OrgAuthProviderConfig by OidcAuthority.
//          Match → Oidc, cache + dispatch. No match → null (logged).
//   5. Dispatch: provider.ResolveAsync(rawCredential, ct).
//   6. Cache invalidation: 5-minute TTL bounds the staleness window
//      after an admin flips a tenant's OrgAuthProviderConfig. Phase 5+
//      adds explicit cache-bust on PUT /iam/orgs/{orgId}/auth-provider.
//
// Why both providers are received via primary ctor (not via
// `IEnumerable<IAuthProvider>`):
//
//   ASP.NET's `AddScoped<IAuthProvider, X>()` is last-wins on multiple
//   registrations — `AddScoped<IAuthProvider, SigilAuthProvider>()` + a
//   future `AddScoped<IAuthProvider, ExternalOidcAuthProvider>()` would
//   resolve to only the second one. The typed-factory approach (this
//   class receives both concretes explicitly) is the framework-safe way
//   to keep both providers injectable.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers;

/// <summary>
///     <see cref="IAuthProviderResolver" /> implementation. Routes a
///     raw bearer credential by JWT <c>iss</c> to the matching
///     <see cref="IAuthProvider" />; caches the (iss → provider) map for
///     5 minutes.
/// </summary>
/// <remarks>
///     <para><b>Scoped lifetime.</b> Mirrors the providers it dispatches
///     to — the resolver itself holds no per-request state beyond the
///     injected dependencies (which are scoped). The cache is a
///     singleton via <see cref="IMemoryCache" />.</para>
///     <para><b>Why Sigil matches by exclusion.</b> The Plexor Sigil
///     signing service (<c>JwtSigningService</c>) always sets
///     <c>iss="plexor"</c> per
///     <see cref="Application.Abstractions.IdentityClaims.IssuerValue" />.
///     No tenant has an <c>OrgAuthProviderConfig</c> row with
///     <c>OidcAuthority="plexor"</c>, so the exclusion is safe —
///     Sigil-issued tokens never touch the DB lookup.</para>
///     <para><b>No <c>private</c> helpers.</b> JWT header peek lives
///     in <see cref="AuthProviderResolverHelpers" /> per project
///     convention.</para>
/// </remarks>
/// <param name="sigilProvider">
///     Local email+password provider. Routed-to when <c>iss == "plexor"</c>.
/// </param>
/// <param name="oidcProvider">
///     External OIDC provider. Routed-to when
///     <c>OrgAuthProviderConfig.OidcAuthority == iss</c>.
/// </param>
/// <param name="realm">
///     Realm DbContext — read <c>OrgAuthProviderConfig</c> on cache miss
///     for the OIDC path.
/// </param>
/// <param name="cache">
///     Process-local memory cache; 5-minute TTL on the (iss →
///     AuthProviderId) map.
/// </param>
/// <param name="logger">Structured logger.</param>
public sealed class AuthProviderResolver(
    SigilAuthProvider sigilProvider,
    ExternalOidcAuthProvider oidcProvider,
    RealmDbContext realm,
    IMemoryCache cache,
    ILogger<AuthProviderResolver> logger) : IAuthProviderResolver
{
    /// <summary>
    ///     Plexor's own JWT issuer value — every Sigil-issued token
    ///     carries this <c>iss</c> claim (set by
    ///     <c>JwtSigningService.IssueInternalAsync</c> via
    ///     <see cref="Application.Abstractions.IdentityClaims.IssuerValue" />).
    ///     Matches by exclusion — no tenant configures an OIDC
    ///     authority at this URL.
    /// </summary>
    private const string SigilIssuerValue = "plexor";

    /// <summary>
    ///     TTL on the (iss → AuthProviderId) cache entry. Bounds the
    ///     staleness window after an admin flips a tenant's
    ///     <c>OrgAuthProviderConfig</c>; Phase 5+ adds an explicit
    ///     cache-bust on PUT.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Prefix on the cache key — namespaced so a future
    ///     <see cref="IMemoryCache" /> sharing the same backing store
    ///     can't collide with another tenant.
    /// </summary>
    private const string CacheKeyPrefix = "plexor.authprovider.";

    /// <inheritdoc />
    public async Task<AuthResolution?> ResolveAsync(
        string rawCredential,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawCredential))
        {
            return null;
        }

        var issuer = AuthProviderResolverHelpers.PeekIssuer(rawCredential);
        if (issuer is null)
        {
            return null;
        }

        var cacheKey = CacheKeyPrefix + issuer;
        if (cache.TryGetValue<AuthProviderId>(cacheKey, out var cachedId) && cachedId is { } providerId)
        {
            return await DispatchAsync(providerId, rawCredential, cancellationToken);
        }

        AuthProviderId resolvedId;
        if (string.Equals(issuer, SigilIssuerValue, StringComparison.Ordinal))
        {
            resolvedId = AuthProviderId.Sigil;
        }
        else
        {
            // External OIDC path — verify the issuer is registered
            // against at least one OrgAuthProviderConfig row. A bare
            // `iss` claim without a matching row is treated as
            // "no provider claims this credential" → 401.
            var config = await realm.OrgAuthProviderConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    config => config.OidcAuthority == issuer,
                    cancellationToken);

            if (config is null)
            {
                logger.LogInformation(
                    "AuthProviderResolver: no OrgAuthProviderConfig for issuer {Issuer}",
                    issuer);
                return null;
            }

            resolvedId = AuthProviderId.Oidc;
        }

        cache.Set(cacheKey, resolvedId, CacheLifetime);

        return await DispatchAsync(resolvedId, rawCredential, cancellationToken);
    }

    /// <summary>
    ///     Hand the credential to the matching provider. The provider
    ///     is responsible for full signature + claim + lifetime
    ///     validation; the resolver only routes.
    /// </summary>
    /// <remarks>
    ///     <para><b>Why a switch instead of an if/else chain.</b>
    ///     <see cref="AuthProviderId.Sigil" /> and
    ///     <see cref="AuthProviderId.Oidc" /> are static readonly
    ///     record instances (not const), so a switch expression
    ///     can't use them as arms (CS9135). A
    ///     <see langword="switch" /> statement with the discriminator
    ///     compared to the static instances reads cleanly and the
    ///     compiler is happy.</para>
    /// </remarks>
    /// <param name="id">Provider discriminator resolved from the
    ///     <c>iss</c> claim.</param>
    /// <param name="credential">Raw bearer credential (compact JWT).</param>
    /// <param name="cancellationToken">Forwarded to the provider.</param>
    /// <exception cref="InvalidOperationException"></exception>
    private Task<AuthResolution?> DispatchAsync(
        AuthProviderId id,
        string credential,
        CancellationToken cancellationToken)
    {
        if (id == AuthProviderId.Sigil)
        {
            return sigilProvider.ResolveAsync(credential, cancellationToken);
        }

        if (id == AuthProviderId.Oidc)
        {
            return oidcProvider.ResolveAsync(credential, cancellationToken);
        }

        throw new InvalidOperationException($"unknown provider id {id}");
    }
}
