// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BearerAuthenticationHandler — ASP.NET Core authentication scheme for
// Plexor's compact JWTs and API keys. Reads the
// `Authorization: Bearer <token>` header, routes JWT-shaped tokens to
// IJwtSigningService and API-key-shaped tokens (kid_xxx.<secret>) to
// IApiKeyAuthenticationService. Produces a 401 challenge on failure.
// Owns the wire protocol between Plexor.Host and the Sigil auth
// pipeline; downstream code interacts with it via the standard
// [Authorize] attribute.
// ==========================================================================

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Bearer scheme handler — JWT + API key. Constructed once per
/// scheme by the authentication middleware; per-request
/// <see cref="HandleAuthenticateAsync" /> reads the header, dispatches
/// to the right verifier, and emits an
/// <see cref="AuthenticateResult" /> that the framework
/// <c>AuthenticationMiddleware</c> maps to <c>HttpContext.User</c>.
/// </summary>
/// <param name="options"></param>
/// <param name="loggerFactory"></param>
/// <param name="urlEncoder"></param>
/// <param name="jwt"></param>
/// <param name="apiKeys"></param>
/// <remarks>
///     <para><b>Token shapes.</b>
///     <list type="bullet">
///       <item>JWT: three base64url segments separated by dots —
///       decoded by <see cref="IJwtSigningService.VerifyAsync" />.</item>
///       <item>API key: <c>kid_&lt;uuid&gt;.&lt;base64url-secret&gt;</c> —
///       routed to <see cref="IApiKeyAuthenticationService.AuthenticateAsync" />.
///       Service-to-service auth (NodeAgent ↔ Host, CI bots).</item>
///     </list></para>
///     <para><b>Error model.</b> <see cref="VerifyResult.Invalid" /> and
///     <see cref="ApiKeyAuthenticationResult.Invalid" /> both map to
///     <see cref="AuthenticateResult.Fail(string)" />. The framework
///     calls <c>HandleChallengeAsync</c> on a 401, which writes
///     <c>WWW-Authenticate: error="invalid_token"</c> per RFC 6750.</para>
/// </remarks>
public sealed class BearerAuthenticationHandler(
    IOptionsMonitor<BearerOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder,
    IJwtSigningService jwt,
    IApiKeyAuthenticationService apiKeys)
    : AuthenticationHandler<BearerOptions>(options, loggerFactory, urlEncoder)
{
    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";

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
        // we look at the first one only — joining them would produce a
        // garbage token that fails verify for the wrong reason.
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

        var cancellationToken = Context.RequestAborted;

        // API keys are prefixed with 'kid_' (RFC-style kid header).
        // JWTs are three base64url segments separated by dots. The
        // presence of dots is the cheapest disambiguator — a kid_*
        // never has dots in the prefix; a JWT always has two.
        return token.Contains('.') switch
        {
            true => await VerifyJwtAsync(token, cancellationToken),
            false => await VerifyApiKeyAsync(token, cancellationToken),
        };
    }

    /// <summary>JWT branch — delegates to <see cref="IJwtSigningService" />.</summary>
    /// <param name="compactJwt"></param>
    /// <param name="cancellationToken"></param>
    private async Task<AuthenticateResult> VerifyJwtAsync(
        string compactJwt,
        CancellationToken cancellationToken)
    {
        var verification = await jwt.VerifyAsync(compactJwt, cancellationToken);

        return verification switch
        {
            VerifyResult.Success success => AuthenticateResult.Success(
                new AuthenticationTicket(
                    success.Principal,
                    BuildAuthenticationProperties(success.Principal),
                    Scheme.Name)),
            VerifyResult.Invalid invalid => AuthenticateResult.Fail(invalid.Reason),
            VerifyResult.Malformed malformed => AuthenticateResult.Fail(malformed.Reason),
            _ => AuthenticateResult.Fail("Unknown verification outcome."),
        };
    }

    /// <summary>
    ///     API-key branch. Expects <c>kid_&lt;uuid&gt;.&lt;secret&gt;</c>;
    ///     unknown shapes (typos, wrong separators) fall through to
    ///     a generic invalid-token failure.
    /// </summary>
    /// <param name="rawToken"></param>
    /// <param name="cancellationToken"></param>
    private async Task<AuthenticateResult> VerifyApiKeyAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (!TryParseApiKeyToken(rawToken, out var keyId, out var secret))
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

    /// <summary>
    ///     Parse <c>kid_&lt;uuid&gt;.&lt;secret&gt;</c> into its two
    ///     parts. <c>kid_</c> prefix is required; UUID must be a real
    ///     <see cref="Guid" />; the secret is the raw base64url portion
    ///     after the dot. Returns <c>false</c> for anything that
    ///     doesn't match.
    /// </summary>
    /// <param name="rawToken"></param>
    /// <param name="keyId"></param>
    /// <param name="secret"></param>
    private static bool TryParseApiKeyToken(
        string rawToken,
        out Guid keyId,
        out string secret)
    {
        keyId = Guid.Empty;
        secret = string.Empty;

        const string kidPrefix = "kid_";
        if (!rawToken.StartsWith(kidPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var dotIndex = rawToken.IndexOf('.');
        if (dotIndex <= kidPrefix.Length)
        {
            return false;
        }

        var kidString = rawToken[kidPrefix.Length..dotIndex];
        if (!Guid.TryParse(kidString, out keyId) || keyId == Guid.Empty)
        {
            return false;
        }

        secret = rawToken[(dotIndex + 1)..];
        return secret.Length > 0;
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

    /// <summary>
    ///     Surface the JWT's <c>iat</c> / <c>exp</c> claims on the
    ///     <see cref="AuthenticationProperties" /> so downstream code
    ///     (sliding sessions, token refresh middleware) can act on
    ///     them without re-parsing the token.
    /// </summary>
    /// <param name="principal">
    ///     The principal returned by <see cref="IJwtSigningService.VerifyAsync" />.
    ///     Claims are read, not mutated.
    /// </param>
    /// <returns>
    ///     A new <see cref="AuthenticationProperties" /> with
    ///     <see cref="AuthenticationProperties.IssuedUtc" /> and
    ///     <see cref="AuthenticationProperties.ExpiresUtc" /> populated
    ///     when the corresponding claims are present.
    /// </returns>
    private static AuthenticationProperties BuildAuthenticationProperties(ClaimsPrincipal principal)
    {
        var properties = new AuthenticationProperties();

        if (TryReadUnixSeconds(principal, IdentityClaims.IssuedAt) is { } issued)
        {
            properties.IssuedUtc = DateTimeOffset.FromUnixTimeSeconds(issued);
        }

        if (TryReadUnixSeconds(principal, IdentityClaims.ExpiresAt) is { } expires)
        {
            properties.ExpiresUtc = DateTimeOffset.FromUnixTimeSeconds(expires);
        }

        return properties;
    }

    /// <summary>
    ///     Reads a numeric claim and parses it as Unix seconds. Returns
    ///     <c>null</c> if the claim is missing or not parseable as a
    ///     long integer — both are non-fatal: the caller proceeds with
    ///     whatever subset of <c>iat</c> / <c>exp</c> was readable.
    /// </summary>
    /// <param name="principal"></param>
    /// <param name="claimType"></param>
    private static long? TryReadUnixSeconds(ClaimsPrincipal principal, string claimType)
    {
        var raw = principal.FindFirstValue(claimType);
        return long.TryParse(
            raw,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }
}
