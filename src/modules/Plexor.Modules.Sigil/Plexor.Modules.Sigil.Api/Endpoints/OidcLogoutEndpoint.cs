// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcLogoutEndpoint — POST /api/v1/auth/oidc/logout. Phase 4.6.3b.
//
// Anonymous; best-effort RP-initiated logout (OpenID Connect Session
// Management 1.0 §5). v1 is a no-op returning 204:
//   - Plexor has no OIDC-issued session cookie today (the v1 token
//     delivery is "?access_token=..." on the deep-link redirect, not
//     a cookie) — there's nothing Plexor-side to clear.
//   - A real RP-initiated logout would POST to the IDP's
//     end_session_endpoint with id_token_hint + post_logout_redirect_uri;
//     Phase 5+ wires that when cookie-based session delivery lands.
//
// Returning 204 unconditionally keeps the client simple: callers can
// fire-and-forget the logout regardless of whether the user is
// actually authenticated. A non-2xx would invite the client to retry
// indefinitely.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Sigil.Api.Endpoints;

/// <summary>
///     Minimal API endpoint that handles RP-initiated logout. Mounted
///     at <c>/api/v1/auth/oidc/logout</c> via
///     <see cref="MapOidcLogout" />.
/// </summary>
public static class OidcLogoutEndpoint
{
    /// <summary>The static path this endpoint exposes.</summary>
    public const string Path = ApiRoutes.Base + "/auth/oidc/logout";

    /// <summary>
    ///     Map the endpoint. Call once from <c>Program.cs</c> at
    ///     composition time.
    /// </summary>
    public static IEndpointRouteBuilder MapOidcLogout(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(Path, HandleAsync);
        return builder;
    }

    /// <summary>
    ///     Endpoint handler. Minimal API endpoint method-group; kept
    ///     private static per <c>code-shape.md §9</c> exception
    ///     (minimal API endpoint handler). Always returns 204.
    /// </summary>
    private static IResult HandleAsync()
    {
        return Results.NoContent();
    }
}