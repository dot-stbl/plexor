// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcAuthorizeEndpoint — GET /api/v1/auth/oidc/authorize. Phase 4.6.3b.
//
// Anonymous; produces a 302 to the tenant's OIDC authority's
// authorization endpoint (Keycloak / Authentik / Dex / ...).
//
// Flow (RFC 6749 §4.1.1 + RFC 7636 §4.3):
//   1. Look up the tenant's OrgAuthProviderConfig via the org query
//      parameter (OrgId GUID for v1; Phase 5+ adds slug → id
//      resolution).
//   2. Reject early (400) if the tenant has no OIDC config — the
//      /auth/login path is the right shape for Sigil tenants.
//   3. Mint a fresh PKCE pair (code_verifier + code_challenge,
//      method=S256).
//   4. Mint an opaque random state token (RFC 6749 §10.12).
//   5. Persist (state → OidcFlowContext) in IOidcFlowStateStore
//      (10-minute TTL, one-shot consumption on the callback leg).
//   6. Build the IDP authorization URL:
//        {authority}/protocol/openid-connect/auth
//          ?response_type=code
//          &client_id={oidcClientId}
//          &redirect_uri={our_callback_url}
//          &scope=openid+profile+email
//          &state={state}
//          &code_challenge={code_challenge}
//          &code_challenge_method=S256
//   7. Results.Redirect(authorizationUrl, permanent: false).
//
// v1 hardcodes the Keycloak-style path
// {authority}/protocol/openid-connect/auth. Phase 5+ reads the
// authorization_endpoint from the discovery document cached by
// JwksFetcher.
// ============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.AuthProviders;

namespace Plexor.Modules.Sigil.Api.Endpoints;

/// <summary>
///     Minimal API endpoint that starts the OIDC authorization-code +
///     PKCE flow. Mounted at <c>/api/v1/auth/oidc/authorize</c> via
///     <see cref="MapOidcAuthorize" />.
/// </summary>
public static class OidcAuthorizeEndpoint
{
    /// <summary>The static path this endpoint exposes. Composed
    /// from <see cref="ApiRoutes.Base" /> + the local segment so
    /// the URL prefix lives in one place (see
    /// <c>api-route-constants.md</c>).</summary>
    public const string Path = ApiRoutes.Base + "/auth/oidc/authorize";

    /// <summary>
    ///     Map the endpoint. Call once from <c>Program.cs</c> at
    ///     composition time. The endpoint is anonymous — the whole
    ///     point is the inbound flow.
    /// </summary>
    public static IEndpointRouteBuilder MapOidcAuthorize(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(Path, HandleAsync);
        return builder;
    }

    /// <summary>
    ///     Endpoint handler. Minimal API endpoint method-group; kept
    ///     private static per <c>code-shape.md §9</c> exception
    ///     (minimal API endpoint handler). Non-trivial string
    ///     composition lives in <see cref="OidcAuthorizeHelpers" />
    ///     (file static class) so the handler stays a thin
    ///     orchestrator.
    /// </summary>
    /// <remarks>
    ///     <para><b>Why the handler resolves the callback URL from
    ///     the request.</b> The IDP requires an exact <c>redirect_uri</c>
    ///     match — we don't want a configurable callback URL drift
    ///     between the authorize and callback legs. The handler reads
    ///     <c>scheme + host</c> from the inbound request and appends
    ///     the well-known callback path; behind a TLS-terminating
    ///     proxy the operator must configure ForwardedHeaders so
    ///     <c>context.Request.Scheme</c> is <c>https</c>.</para>
    /// </remarks>
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string org,
        string redirect,
        IOidcFlowStateStore stateStore,
        IPkceGenerator pkce,
        IOrgAuthProviderConfigReader configReader,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Plexor.Modules.Sigil.Api.Endpoints.OidcAuthorizeEndpoint");

        if (!Guid.TryParse(org, out var orgId))
        {
            logger.LogWarning(
                "OidcAuthorizeEndpoint: org query parameter {Org} is not a valid GUID",
                org);
            return TypedResults.Problem(
                title: "Invalid org parameter",
                detail: "The org query parameter must be a valid organization id (GUID).",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.invalid_org" });
        }

        var config = await configReader.GetForOrgAsync(orgId, cancellationToken);
        if (config is null || config.Provider != OrgAuthProvider.Oidc
            || string.IsNullOrEmpty(config.OidcAuthority)
            || string.IsNullOrEmpty(config.OidcClientId))
        {
            logger.LogWarning(
                "OidcAuthorizeEndpoint: org {OrgId} has no OIDC config",
                orgId);
            return TypedResults.Problem(
                title: "OIDC not configured",
                detail: "The organization is not configured for external OIDC.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "oidc.not_configured" });
        }

        var pkcePair = pkce.Generate();
        var state = TokenGenerator.Generate();

        var callbackUri = OidcAuthorizeHelpers.BuildCallbackUri(context, logger);
        var originalRedirect = OidcAuthorizeHelpers.NormaliseRedirect(redirect);

        stateStore.Issue(
            state,
            new OidcFlowContext(
                OrgId: orgId,
                CodeVerifier: pkcePair.CodeVerifier,
                RedirectUri: callbackUri,
                OriginalRedirect: originalRedirect));

        var authorizationUrl = OidcAuthorizeHelpers.BuildAuthorizationUrl(
            config.OidcAuthority,
            config.OidcClientId,
            callbackUri,
            config.OidcScopes,
            state,
            pkcePair.CodeChallenge);

        logger.LogInformation(
            "OidcAuthorizeEndpoint: redirecting to OIDC for org {OrgId}",
            orgId);

        return Results.Redirect(authorizationUrl, permanent: false);
    }
}

/// <summary>
///     Pure helpers for <see cref="OidcAuthorizeEndpoint" /> — kept
///     in a file-scope class so they don't pollute the public surface
///     of the endpoint file (per <c>code-shape.md §9.10</c>).
/// </summary>
file static class OidcAuthorizeHelpers
{
    /// <summary>Keycloak's standard authorization-endpoint path. v1
    /// hardcodes this for the Keycloak / Authentik / Dex shape; Auth0
    /// and Azure AD diverge. Phase 5+ reads
    /// <c>authorization_endpoint</c> from the discovery document.</summary>
    private const string KeycloakAuthorizeEndpointPath = "/protocol/openid-connect/auth";

    /// <summary>
    ///     Build the callback URL from the inbound request. The
    ///     <c>redirect_uri</c> sent to the IDP MUST match what the
    ///     IDP has registered for this client (RFC 6749 §10.6).
    /// </summary>
    public static string BuildCallbackUri(HttpContext context, ILogger logger)
    {
        var scheme = context.Request.Scheme;
        var host = context.Request.Host;

        var callbackUri = $"{scheme}://{host}{ApiRoutes.Base}/auth/oidc/callback";
        logger.LogDebug(
            "OidcAuthorizeEndpoint: built callback URI {Callback}",
            callbackUri);
        return callbackUri;
    }

    /// <summary>
    ///     Normalise the operator-supplied deep-link. We accept any
    ///     path-shaped string and trust the caller — the value flows
    ///     into the redirect URL on the callback leg, and the
    ///     operator's frontend is responsible for sanitising its own
    ///     internal routes.
    /// </summary>
    public static string NormaliseRedirect(string redirect)
    {
        if (string.IsNullOrWhiteSpace(redirect))
        {
            return "/";
        }

        return redirect.StartsWith('/') ? redirect : "/" + redirect;
    }

    /// <summary>
    ///     Compose the OIDC authorization URL. Keycloak-shaped
    ///     for v1; Phase 5+ reads the path from the discovery
    ///     document.
    /// </summary>
    public static string BuildAuthorizationUrl(
        string authority,
        string clientId,
        string redirectUri,
        IReadOnlyList<string> scopes,
        string state,
        string codeChallenge)
    {
        var authorizeEndpoint = new Uri(new Uri(authority), KeycloakAuthorizeEndpointPath);
        var scope = string.Join('+', scopes);

        var query = new[]
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"scope={Uri.EscapeDataString(scope)}",
            $"state={Uri.EscapeDataString(state)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
        };

        return $"{authorizeEndpoint}?{string.Join('&', query)}";
    }
}