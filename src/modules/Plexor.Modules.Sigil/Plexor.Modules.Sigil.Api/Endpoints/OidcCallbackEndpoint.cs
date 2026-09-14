// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcCallbackEndpoint — GET /api/v1/auth/oidc/callback. Phase 4.6.3b.
//
// Anonymous; this is where the IDP returns the operator after the
// authorization-code + PKCE round-trip. Validates state against the
// server-side store, exchanges the code for tokens at the IDP's
// token endpoint, then (in 4.6.3c) provisions / finds the Plexor
// User row and mints a Plexor bearer. Phase 4.6.3b stops at the
// provisioning gate with a 501 — the auth wiring itself is correct
// end-to-end; user provisioning is the only missing step.
//
// Flow (RFC 6749 §4.1.2 + RFC 7636 §4.6):
//   1. Read `state` and `code` from the IDP's redirect query.
//   2. Look up + one-shot consume OidcFlowContext via
//      stateStore.Consume(state). Unknown / expired / replayed
//      → 400 invalid_state.
//   3. Exchange code via IOidcTokenClient.ExchangeCodeAsync(orgId,
//      code, codeVerifier, redirectUri). Failure → 400
//      token_exchange_failed (the OidcTokenClient collapses every
//      failure path into a null return).
//   4. Provision / find the Plexor User row — OUT OF SCOPE for
//      4.6.3b. Returns 501 with a clear "this is the wiring, the
//      provisioning lands in 4.6.3c" message. 4.6.3c replaces this
//      step with the actual Sigil-user upsert.
//   5. Mint a Plexor access token via ITokenIssuer.IssueAsync
//      (4.6.3c).
//   6. Redirect to {OriginalRedirect}?access_token={token} (4.6.3c).
//
// The endpoint is anonymous — the whole point is the inbound flow.
// The bearer token is delivered as a URL query parameter for v1;
// Phase 5+ replaces this with a secure session cookie so the
// access token never appears in browser history.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.AuthProviders;

namespace Plexor.Modules.Sigil.Api.Endpoints;

/// <summary>
///     Minimal API endpoint that finishes the OIDC authorization-code +
///     PKCE flow. Mounted at <c>/api/v1/auth/oidc/callback</c> via
///     <see cref="MapOidcCallback" />.
/// </summary>
public static class OidcCallbackEndpoint
{
    /// <summary>The static path this endpoint exposes.</summary>
    public const string Path = ApiRoutes.Base + "/auth/oidc/callback";

    /// <summary>
    ///     Map the endpoint. Call once from <c>Program.cs</c> at
    ///     composition time.
    /// </summary>
    public static IEndpointRouteBuilder MapOidcCallback(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(Path, HandleAsync);
        return builder;
    }

    /// <summary>
    ///     Endpoint handler. Minimal API endpoint method-group; kept
    ///     private static per <c>code-shape.md §9</c> exception
    ///     (minimal API endpoint handler).
    /// </summary>
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string? state,
        string? code,
        IOidcFlowStateStore stateStore,
        IOidcTokenClient tokenClient,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Plexor.Modules.Sigil.Api.Endpoints.OidcCallbackEndpoint");

        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
        {
            logger.LogWarning(
                "OidcCallbackEndpoint: missing state or code query parameter");
            return TypedResults.Problem(
                title: "Invalid callback",
                detail: "The callback request is missing required query parameters.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.invalid_callback" });
        }

        var flowContext = stateStore.Consume(state);
        if (flowContext is null)
        {
            logger.LogWarning(
                "OidcCallbackEndpoint: state {State} is unknown, expired, or already consumed",
                state);
            return TypedResults.Problem(
                title: "Invalid state",
                detail: "The state token is unknown, expired, or already consumed.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.invalid_state" });
        }

        var tokenResponse = await tokenClient.ExchangeCodeAsync(
            flowContext.OrgId,
            code,
            flowContext.CodeVerifier,
            flowContext.RedirectUri,
            cancellationToken);

        if (tokenResponse is null)
        {
            logger.LogWarning(
                "OidcCallbackEndpoint: token exchange failed for org {OrgId}",
                flowContext.OrgId);
            return TypedResults.Problem(
                title: "Token exchange failed",
                detail: "The IDP rejected the authorization code. Start the flow again.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.token_exchange_failed" });
        }

        // 4.6.3b stops here. Steps 5-6 (User provisioning +
        // Plexor access token mint + deep-link redirect) land in
        // 4.6.3c — the call chain is wired, the only missing piece
        // is the per-org Sigil user row upsert. Return 501 with a
        // clear message so the wiring can be smoke-tested without
        // the provisioning story being a black box. The IdToken is
        // intentionally NOT echoed in the response (PII / token
        // leakage).
        logger.LogInformation(
            "OidcCallbackEndpoint: token exchange succeeded for org {OrgId}; user provisioning is the next step (4.6.3c)",
            flowContext.OrgId);

        return TypedResults.Problem(
            title: "User provisioning not yet implemented",
            detail: "OIDC token exchange succeeded. User provisioning + Plexor token mint lands in 4.6.3c.",
            statusCode: StatusCodes.Status501NotImplemented,
            extensions: new Dictionary<string, object?> { ["code"] = "oidc.provisioning_pending" });
    }
}