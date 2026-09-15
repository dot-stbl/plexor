// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BearerAuthenticationHandler — ASP.NET Core authentication scheme for
// Plexor's bearer credentials (Sigil JWT + OIDC JWT + API keys).
// Reads the `Authorization: Bearer <token>` header, routes JWT-shaped
// tokens through IAuthProviderResolver (the dispatch boundary for the
// dual auth-provider layer) and API-key-shaped tokens
// (`kid_xxx.<secret>`) to IApiKeyAuthenticationService. Produces a 401
// challenge on failure.
//
// Phase 4.6.2c rewrites the JWT path:
//
//   v0.5 (pre-resolver): JWT → IJwtSigningService.VerifyAsync → principal.
//   v0.6 (this commit):  JWT → IAuthProviderResolver.ResolveAsync →
//                          AuthResolution → principal rebuilt from claims.
//
// The dispatcher's job is to peek the JWT `iss` claim, route to the
// right IAuthProvider (Sigil for `iss="plexor"`, ExternalOidc for
// anything else registered against an OrgAuthProviderConfig row), and
// return an AuthResolution. This handler builds the canonical
// ClaimsPrincipal from the resolution — the same `sub` / `tid` / `iss` /
// `role` / `permission` / `service` claim shape the rest of the
// authorization pipeline already reads.
//
// API keys remain unchanged (4.6.2c scope explicitly excludes them —
// the existing IApiKeyAuthenticationService contract is preserved).
// ==========================================================================

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers;
using Plexor.Modules.Sigil.Infrastructure.CurrentUser;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Bearer scheme handler — JWT (Sigil + OIDC) + API key.
///     Constructed once per scheme by the authentication middleware;
///     per-request <see cref="HandleAuthenticateAsync" /> reads the
///     header, dispatches to the right verifier, and emits an
///     <see cref="AuthenticateResult" /> the framework
///     <c>AuthenticationMiddleware</c> maps to <c>HttpContext.User</c>.
/// </summary>
/// <param name="options"></param>
/// <param name="loggerFactory"></param>
/// <param name="urlEncoder"></param>
/// <param name="resolver">Phase 4.6.2c — the auth provider resolver;
///     routes a raw JWT bearer credential to the right
///     <see cref="IAuthProvider" />.</param>
/// <param name="apiKeys">Existing API-key path. The handler delegates
///     <c>kid_xxx.&lt;secret&gt;</c> tokens to this service; the
///     contract is unchanged.</param>
/// <remarks>
///     <para><b>Token shapes.</b>
///     <list type="bullet">
///       <item>JWT: three base64url segments separated by dots —
///       routed to <see cref="IAuthProviderResolver.ResolveAsync" />
///       which dispatches to
///       <see cref="Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers.SigilAuthProvider" /> or
///       <see cref="ExternalOidcAuthProvider" /> based on the
///       <c>iss</c> claim.</item>
///       <item>API key: <c>kid_&lt;uuid&gt;.&lt;base64url-secret&gt;</c>
///       — routed to
///       <see cref="IApiKeyAuthenticationService.AuthenticateAsync" />.
///       Service-to-service auth (NodeAgent ↔ Host, CI bots).</item>
///     </list></para>
///     <para><b>Claim shape parity.</b>
///     The principal built from <see cref="AuthResolution" /> mirrors
///     the claim shape the Sigil JWT signer wrote for v0.5:
///     <c>sub</c> / <c>tid</c> / <c>iss</c> / <c>service</c> /
///     <c>role</c>[] / <c>permission</c>[]. <c>[RequirePermission(...)]</c>
///     on controllers reads <c>permission</c> claims identically;
///     <see cref="HttpContextCurrentUser" /> reads <c>sub</c> /
///     <c>tid</c> / <c>service</c> identically. Any drift here
///     breaks downstream authorization silently — see
///     <see cref="IdentityClaims" /> for the constants.</para>
///     <para><b>iat / exp gap.</b> The v0.5 handler surfaced
///     <see cref="AuthenticationProperties.IssuedUtc" /> and
///     <see cref="AuthenticationProperties.ExpiresUtc" /> from the
///     JWT's <c>iat</c> / <c>exp</c> claims. The new
///     <see cref="AuthResolution" /> record carries <c>TokenLifetime</c>
///     but not the raw claim values; the principal built here does
///     not include <c>iat</c> / <c>exp</c>. No code currently
///     consumes <see cref="AuthenticationProperties.IssuedUtc" /> /
///     <c>ExpiresUtc</c> — the gap is documented; Phase 4.6.3 or
///     later can add an <c>IssuedAt</c> / <c>ExpiresAt</c> field to
///     <see cref="AuthResolution" /> if sliding-session middleware
///     needs them.</para>
///     <para><b>Error model.</b> <c>null</c> from the resolver maps
///     to <see cref="AuthenticateResult.Fail(string)" />. The
///     framework calls <see cref="HandleChallengeAsync" /> on a 401,
///     which writes <c>WWW-Authenticate: error="invalid_token"</c>
///     per RFC 6750.</para>
///     <para><b>No <c>private</c> methods.</b> API-key parsing lives
///     in <see cref="BearerAuthenticationHandlerHelpers" /> per
///     project convention <c>code-shape.md §9</c>.</para>
/// </remarks>
public sealed class BearerAuthenticationHandler(
    IOptionsMonitor<BearerOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder,
    IAuthProviderResolver resolver,
    IApiKeyAuthenticationService apiKeys)
    : AuthenticationHandler<BearerOptions>(options, loggerFactory, urlEncoder)
{
    private const string AuthorizationHeader = "Authorization";

    private const string BearerPrefix = "Bearer ";

    private const string ApiKeyPrefix = "kid_";

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthorizationHeader, out var headerValues)
            || headerValues.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        // Authorization is a single-value header per RFC 7235. If the
        // client sent multiple values (comma-joined or repeated headers)
        // we look at the first one only — joining them would produce
        // a garbage token that fails verify for the wrong reason.
        var raw = headerValues[0];
        if (string.IsNullOrEmpty(raw) || !raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = raw[BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.Fail("Bearer token is empty.");
        }

        // API keys start with `kid_` (RFC-style kid header). JWTs are
        // three base64url segments separated by dots. The prefix check
        // is cheaper than the dot-count check and unambiguous — kid_*
        // never has dots in the prefix, a JWT never starts with kid_.
        if (token.StartsWith(ApiKeyPrefix, StringComparison.Ordinal))
        {
            return await AuthenticateApiKeyAsync(token, Context.RequestAborted);
        }

        return await AuthenticateJwtAsync(token, Context.RequestAborted);
    }

    /// <summary>
    ///     JWT branch — delegates to <see cref="IAuthProviderResolver" />.
    ///     The resolver peeks the <c>iss</c> claim, dispatches to the
    ///     right provider (Sigil / ExternalOidc), and returns the
    ///     resolved principal data as an <see cref="AuthResolution" />.
    ///     <c>null</c> from the resolver (unknown issuer, invalid
    ///     signature, expired token, suspended user) maps to a 401.
    /// </summary>
    /// <param name="compactJwt">Compact JWT (header.payload.signature).</param>
    /// <param name="cancellationToken">Forwarded to the resolver.</param>
    private async Task<AuthenticateResult> AuthenticateJwtAsync(
        string compactJwt,
        CancellationToken cancellationToken)
    {
        var resolution = await resolver.ResolveAsync(compactJwt, cancellationToken);
        if (resolution is null)
        {
            return AuthenticateResult.Fail("invalid_token");
        }

        return AuthenticateResult.Success(
            new AuthenticationTicket(
                BearerAuthenticationHandlerHelpers.BuildPrincipal(resolution, Scheme.Name),
                new AuthenticationProperties(),
                Scheme.Name));
    }

    /// <summary>
    ///     API-key branch — unchanged from the v0.5 handler. The
    ///     contract is owned by
    ///     <see cref="IApiKeyAuthenticationService" />; the handler
    ///     only dispatches.
    /// </summary>
    /// <param name="rawToken">Compact API key (kid_xxx.&lt;secret&gt;).</param>
    /// <param name="cancellationToken">Forwarded to the service.</param>
    private async Task<AuthenticateResult> AuthenticateApiKeyAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (!BearerAuthenticationHandlerHelpers.TryParseApiKeyToken(
            rawToken, out var keyId, out var secret))
        {
            return AuthenticateResult.Fail("Malformed API key.");
        }

        var result = await apiKeys.AuthenticateAsync(keyId, secret, cancellationToken);
        return result switch
        {
            ApiKeyAuthenticationResult.Success success => AuthenticateResult.Success(
                new AuthenticationTicket(
                    success.Principal,
                    new AuthenticationProperties(),
                    Scheme.Name)),
            ApiKeyAuthenticationResult.NotFound => AuthenticateResult.Fail(
                "API key not found."),
            ApiKeyAuthenticationResult.Invalid invalid => AuthenticateResult.Fail(
                invalid.Reason),
            _ => AuthenticateResult.Fail("Unknown API key outcome."),
        };
    }

    /// <inheritdoc />
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var realm = Options.Realm;
        // RFC 6750 §3: a Bearer challenge carries error="invalid_token"
        // for missing / invalid / expired access tokens. We include the
        // code unconditionally (it's safe — no info leak); error_description
        // is intentionally omitted because it can reveal why a token
        // failed (timing / fuzzing attacks).
        Response.Headers.WWWAuthenticate =
            $"{BearerOptions.SchemeName} realm=\"{realm}\", error=\"invalid_token\"";
        await base.HandleChallengeAsync(properties);
    }
}
