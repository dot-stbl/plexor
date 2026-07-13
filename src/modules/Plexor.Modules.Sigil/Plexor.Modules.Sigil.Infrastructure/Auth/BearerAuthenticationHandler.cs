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
        if (!Request.Headers.TryGetValue(AuthorizationHeader, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var raw = headerValues.ToString();
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
                    new AuthenticationProperties(),
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
        Response.Headers.WWWAuthenticate = $"{BearerOptions.SchemeName} realm=\"{realm}\"";
        await base.HandleChallengeAsync(properties);
    }
}
