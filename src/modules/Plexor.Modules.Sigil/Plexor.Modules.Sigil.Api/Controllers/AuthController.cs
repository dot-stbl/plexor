// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthController — /auth/login, /auth/refresh, /auth/logout, /auth/me.
// Anonymous endpoints (login + refresh + logout); /auth/me requires
// a valid bearer token.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Sigil.Api.Controllers;

/// <summary>
///     Authentication endpoints for the Sigil module. Sits under
///     <c>/api/v1/auth/*</c> via <see cref="ApiRoutes.Base" />.
///     Returns RFC 7807 ProblemDetails on failure via the global
///     exception handler.
/// </summary>
/// <remarks>
///     <para><b>Anonymous access.</b> Login, refresh, and logout
///     must be reachable without a token — they're how a token is
///     obtained or retired. <see cref="MeAsync" /> is the only
///     endpoint that requires a valid bearer token.</para>
/// </remarks>
[ApiController]
[Route($"{ApiRoutes.Base}/auth")]
[Tags(["auth"])]
[Produces("application/json")]
public sealed class AuthController(
    LoginCommandHandler loginHandler,
    RefreshCommandHandler refreshHandler,
    LogoutCommandHandler logoutHandler,
    MeQueryHandler meHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /auth/login</c> — verify credentials, issue
    ///     access + refresh tokens.
    /// </summary>
    /// <param name="request">Login payload: orgId + (email or username) + password.</param>
    /// <param name="cancellationToken">Forwarded to the handler.</param>
    /// <returns>
    ///     200 OK with <see cref="LoginResult" /> on success; 400/401
    ///     /423 ProblemDetails on failure (via IdentityExceptionHandler).
    /// </returns>
    [HttpPost("login", Name = "auth-login")]
    [EndpointSummary("Verify credentials and issue access + refresh tokens")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await loginHandler.HandleAsync(
            new LoginCommand(
                request.OrgId,
                request.Email,
                request.Username,
                request.Password),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    ///     <c>POST /auth/refresh</c> — rotate the presented refresh
    ///     token and issue a fresh access token.
    /// </summary>
    /// <param name="request">Refresh payload: refreshToken string.</param>
    /// <param name="cancellationToken">Forwarded to the handler.</param>
    /// <returns>
    ///     200 OK with <see cref="LoginResult" /> on success; 401
    ///     ProblemDetails on replay / not-found (the whole family is
    ///     revoked on replay — caller must sign in again).
    /// </returns>
    [HttpPost("refresh", Name = "auth-refresh")]
    [EndpointSummary("Rotate a refresh token and re-issue an access token")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> RefreshAsync(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await refreshHandler.HandleAsync(
            new RefreshCommand(request.RefreshToken),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    ///     <c>POST /auth/logout</c> — revoke the presented refresh
    ///     token. Idempotent.
    /// </summary>
    /// <param name="request">Logout payload: refreshToken string.</param>
    /// <param name="cancellationToken">Forwarded to the handler.</param>
    /// <returns>
    ///     200 OK with <see cref="LogoutResult" />. Always succeeds
    ///     — even with an unknown or already-revoked token — so
    ///     callers can't probe which tokens are alive.
    /// </returns>
    [HttpPost("logout", Name = "auth-logout")]
    [EndpointSummary("Revoke a refresh token (idempotent)")]
    [AllowAnonymous]
    public async Task<ActionResult<LogoutResult>> LogoutAsync(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await logoutHandler.HandleAsync(
            new LogoutCommand(request.RefreshToken),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    ///     <c>GET /auth/me</c> — return the authenticated caller's
    ///     identity, roles, and permissions.
    /// </summary>
    /// <param name="cancellationToken">Forwarded to the handler.</param>
    /// <returns>
    ///     200 OK with <see cref="MeResult" /> populated from the
    ///     JWT claims (no DB hit). 401 ProblemDetails if the caller
    ///     is anonymous.
    /// </returns>
    [HttpGet("me", Name = "auth-me")]
    [EndpointSummary("Return the authenticated caller's identity, roles, and permissions")]
    [Authorize]
    public async Task<ActionResult<MeResult>> MeAsync(
        CancellationToken cancellationToken)
    {
        var result = await meHandler.HandleAsync(new MeQuery(), cancellationToken);
        return Ok(result);
    }
}

/// <summary>
///     Wire shape for <c>POST /auth/login</c>. Either
///     <see cref="Email" /> or <see cref="Username" /> must be
///     supplied (both null → 400).
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Email">Email address (mutually exclusive with Username).</param>
/// <param name="Username">Username (mutually exclusive with Email).</param>
/// <param name="Password">Plain-text password.</param>
public sealed record LoginRequest(
    Guid OrgId,
    string? Email,
    string? Username,
    string Password);

/// <summary>Wire shape for <c>POST /auth/refresh</c>.</summary>
/// <param name="RefreshToken">The opaque refresh token returned at login.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Wire shape for <c>POST /auth/logout</c>.</summary>
/// <param name="RefreshToken">The opaque refresh token to revoke. May be empty.</param>
public sealed record LogoutRequest(string? RefreshToken);
