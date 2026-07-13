// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BearerAuthenticationHandler — ASP.NET Core authentication scheme for
// Plexor's compact JWTs. Reads the `Authorization: Bearer <jwt>` header,
// delegates verification to IJwtSigningService, and produces a 401
// challenge on failure. Owns the wire protocol between Plexor.Host
// and the Sigil auth pipeline; downstream code interacts with it via
// the standard [Authorize] attribute.
// ============================================================================

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     JWT bearer scheme handler. Constructed once per scheme by the
///     authentication middleware; per-request <see cref="HandleAuthenticateAsync" />
///     reads the header, validates the token, and emits an
///     <see cref="AuthenticateResult" /> that the framework
///     <c>AuthenticationMiddleware</c> maps to <c>HttpContext.User</c>.
/// </summary>
/// <remarks>
///     <para><b>Async surface.</b> Overrides are async by signature
///     (the framework awaits them). Cancellation is forwarded from the
///     request abort token via <see cref="Microsoft.AspNetCore.Http.HttpContext.RequestAborted" />.</para>
///     <para><b>Error model.</b> <see cref="VerifyResult.Invalid" /> and
///     <see cref="VerifyResult.Malformed" /> both map to
///     <see cref="AuthenticateResult.Fail(string)" />. We do NOT call
///     <c>HandleChallengeAsync</c> from within the handler — the
///     authentication middleware does that automatically when the auth
///     service returns a <c>Fail</c> on a request protected by
///     <c>[Authorize]</c>.</para>
/// </remarks>
public sealed class BearerAuthenticationHandler(
    IOptionsMonitor<BearerOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder,
    IJwtSigningService jwt)
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
        var verification = await jwt.VerifyAsync(token, cancellationToken);

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
