// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcCallbackEndpoint — GET /api/v1/auth/oidc/callback. Phase 4.6.3c.
//
// Anonymous; this is where the IDP returns the operator after the
// authorization-code + PKCE round-trip. Validates state against the
// server-side store, exchanges the code for tokens at the IDP's
// token endpoint, validates the returned id_token, find-or-creates
// the Plexor User row, and mints a Plexor bearer that the operator's
// deep-link redirect consumes as ?access_token=....
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
//   4. Validate the id_token (signature + iss + aud + lifetime)
//      via IOidcIdTokenValidator. Failure → 400 invalid_id_token.
//   5. Find-or-create the Plexor User row via IOidcUserProvisioner
//      (handles RoleBinding creation atomically).
//   6. Resolve the user's roles and mint a Plexor bearer via
//      ITokenIssuer.IssueAsync.
//   7. Redirect to {OriginalRedirect}?access_token={bearer}.
//
// The endpoint is anonymous — the whole point is the inbound flow.
// The bearer token is delivered as a URL query parameter for v1;
// Phase 5+ replaces this with a secure session cookie so the
// access token never appears in browser history.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
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
    /// <param name="builder"></param>
    public static IEndpointRouteBuilder MapOidcCallback(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(Path, HandleAsync);
        return builder;
    }

    /// <summary>
    ///     Endpoint handler. Minimal API endpoint method-group; kept
    ///     private static per <c>code-shape.md §9</c> exception
    ///     (minimal API endpoint handler). Non-trivial composition
    ///     (the deep-link URL + access-token query string) lives
    ///     in <see cref="OidcCallbackHelpers" /> (file static class).
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="code"></param>
    /// <param name="stateStore"></param>
    /// <param name="tokenClient"></param>
    /// <param name="idTokenValidator"></param>
    /// <param name="userProvisioner"></param>
    /// <param name="orgConfigReader"></param>
    /// <param name="roleResolver"></param>
    /// <param name="permissionResolver"></param>
    /// <param name="tokenIssuer"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="cancellationToken"></param>
    /// <remarks>
    ///     <para><b>Why the handler resolves the per-tenant
    ///     config.</b> The id_token's <c>iss</c> claim must match
    ///     the per-tenant authority — the validator carries out
    ///     that check, but it needs the
    ///     <c>OrgAuthProviderConfig</c> as input. The endpoint
    ///     resolves it once via
    ///     <see cref="IOrgAuthProviderConfigReader.GetForOrgAsync" />
    ///     and threads it through both the validator and the
    ///     provisioner.</para>
    ///     <para><b>Why roles are re-resolved via
    ///     <c>IRoleResolver</c>.</b> Newly-provisioned users have
    ///     the <c>viewer</c> role binding (provisioned
    ///     atomically with the User row); existing users carry
    ///     whatever bindings an admin has set. Re-resolving keeps
    ///     the bearer in sync with the latest bindings — the
    ///     admin can promote / demote without forcing a re-login.</para>
    /// </remarks>
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string? state,
        string? code,
        IOidcFlowStateStore stateStore,
        IOidcTokenClient tokenClient,
        IOidcIdTokenValidator idTokenValidator,
        IOidcUserProvisioner userProvisioner,
        IOrgAuthProviderConfigReader orgConfigReader,
        IRoleResolver roleResolver,
        IPermissionResolver permissionResolver,
        ITokenIssuer tokenIssuer,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Plexor.Modules.Sigil.Api.Endpoints.OidcCallbackEndpoint");

        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
        {
            logger.LogWarning(
                "OidcCallbackEndpoint: missing state or code query parameter");
            return TypedResults.Problem(
                detail: "The callback request is missing required query parameters.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid callback",
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.invalid_callback" });
        }

        var flowContext = stateStore.Consume(state);
        if (flowContext is null)
        {
            logger.LogWarning(
                "OidcCallbackEndpoint: state {State} is unknown, expired, or already consumed",
                state);
            return TypedResults.Problem(
                detail: "The state token is unknown, expired, or already consumed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid state",
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
                detail: "The IDP rejected the authorization code. Start the flow again.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token exchange failed",
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.token_exchange_failed" });
        }

        var orgConfig = await orgConfigReader.GetForOrgAsync(
            flowContext.OrgId, cancellationToken);
        if (orgConfig is null || orgConfig.Provider != OrgAuthProvider.Oidc)
        {
            logger.LogWarning(
                "OidcCallbackEndpoint: org {OrgId} no longer has OIDC config after code exchange",
                flowContext.OrgId);
            return TypedResults.Problem(
                detail: "The organization is no longer configured for external OIDC.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "OIDC not configured",
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.not_configured" });
        }

        // Project the Realm entity into the Application-layer DTO so
        // the validator + provisioner stay decoupled from the Realm
        // module. The DTO carries only the four fields they need.
        var tenantConfig = new OidcTenantConfig(
            OrgId: orgConfig.OrgId,
            Authority: orgConfig.OidcAuthority ?? string.Empty,
            ClientId: orgConfig.OidcClientId ?? string.Empty);

        var idTokenClaims = await idTokenValidator.ValidateAsync(
            tokenResponse.IdToken, tenantConfig, cancellationToken);
        if (idTokenClaims is null)
        {
            logger.LogWarning(
                "OidcCallbackEndpoint: id_token validation failed for org {OrgId}",
                flowContext.OrgId);
            return TypedResults.Problem(
                detail: "The IDP-issued id_token failed signature, audience, or lifetime validation.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid id_token",
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.invalid_id_token" });
        }

        // Find-or-create the Plexor User row + viewer-role binding.
        // The provisioner throws OidcUserMissingClaims when the
        // email is missing; let it bubble up to the global error
        // handler (mapped to 400 oidc.user.missing_claims).
        var plexorUser = await userProvisioner.ProvisionAsync(
            tenantConfig,
            idTokenClaims.Subject,
            idTokenClaims.Email ?? string.Empty,
            idTokenClaims.PreferredUsername,
            idTokenClaims.Name,
            cancellationToken);

        var roles = await roleResolver.ResolveAsync(
            plexorUser.Id, plexorUser.OrgId, cancellationToken);
        _ = await permissionResolver.ResolveAsync(
            plexorUser.Id, plexorUser.OrgId, cancellationToken);

        var access = await tokenIssuer.IssueAsync(
            plexorUser.Id, plexorUser.OrgId, roles, cancellationToken);

        logger.LogInformation(
            "OidcCallbackEndpoint: provisioned user {UserId} for org {OrgId}; issuing Plexor bearer",
            plexorUser.Id,
            flowContext.OrgId);

        var deepLink = OidcCallbackHelpers.BuildDeepLink(
            flowContext.OriginalRedirect, access.CompactJwt);

        return Results.Redirect(deepLink, permanent: false);
    }
}

/// <summary>
///     Pure helpers for <see cref="OidcCallbackEndpoint" /> — kept
///     in a file-scope class so they don't pollute the public
///     surface of the endpoint file (per <c>code-shape.md §9.10</c>).
/// </summary>
file static class OidcCallbackHelpers
{
    /// <summary>
    ///     Compose the deep-link redirect URL the operator's
    ///     browser ends up on after the IDP roundtrip. The
    ///     Plexor bearer is appended as <c>?access_token=...</c>
    ///     — Phase 5+ replaces this with a secure session cookie
    ///     so the access token never appears in browser history.
    /// </summary>
    /// <param name="originalRedirect">Path the operator wanted
    /// to land on (from the <c>redirect</c> query param on
    /// <c>GET /auth/oidc/authorize</c>).</param>
    /// <param name="accessToken">Compact JWT.</param>
    public static string BuildDeepLink(string originalRedirect, string accessToken)
    {
        var separator = originalRedirect.Contains('?') ? "&" : "?";
        return $"{originalRedirect}{separator}access_token={Uri.EscapeDataString(accessToken)}";
    }
}
