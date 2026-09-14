// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcTokenClient — IOidcTokenClient implementation (Phase 4.6.3a).
// Calls the per-tenant IDP's token endpoint to exchange an
// authorization code for tokens (RFC 6749 §4.1.3 + RFC 7636 §4.6).
//
// Flow:
//   1. Resolve the OrgAuthProviderConfig for the inbound org.
//   2. Reject when the tenant isn't on the Oidc provider
//      (return null — caller logs + 502s).
//   3. Decrypt the per-tenant OIDC client secret via
//      OrgAuthProviderSecretProtector.
//   4. Mint an HTTP Basic auth header from clientId:secret.
//   5. POST {authority}/protocol/openid-connect/token with
//      application/x-www-form-urlencoded body:
//         grant_type=authorization_code
//         code=<callback code>
//         redirect_uri=<callback redirect_uri>
//         client_id=<oidcClientId>
//         code_verifier=<PKCE verifier>
//   6. Read the response, project to OidcTokenResponse. Return
//      null on any failure path (transport, 4xx/5xx, malformed
//      JSON, missing access_token).
//
// v1 hardcodes the Keycloak-style path
// {authority}/protocol/openid-connect/token. Phase 5+ reads
// token_endpoint from the discovery document cached by
// JwksFetcher.
// ============================================================================

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.AuthProviders;
using Plexor.Shared.Kernel.AuthProviders;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders;

/// <summary>
///     <see cref="IOidcTokenClient" /> implementation. Calls the
///     per-tenant IDP's token endpoint via the named
///     <c>Plexor-OidcDiscovery</c> <see cref="IHttpClientFactory" />
///     client (registered in <c>Plexor.Host/Program.cs</c>; 10s
///     timeout, no retries).
/// </summary>
/// <param name="httpClientFactory">
/// ASP.NET Core HTTP client factory. The client always names the
/// <c>Plexor-OidcDiscovery</c> client explicitly so no future
/// typed-client registration can poison the token-exchange path.
/// </param>
/// <param name="configReader">
/// Reads the per-tenant <see cref="OrgAuthProviderConfig" /> (issuer
/// URL, client id, encrypted client secret). Scoped — same lifetime
/// as the underlying <c>RealmDbContext</c>.
/// </param>
/// <param name="secretProtector">
/// Decrypts <see cref="OrgAuthProviderConfig.OidcClientSecretProtected" />
/// for the outbound Basic auth header. Singleton.
/// </param>
/// <param name="logger">Structured logger.</param>
public sealed class OidcTokenClient(
    IHttpClientFactory httpClientFactory,
    IOrgAuthProviderConfigReader configReader,
    OrgAuthProviderSecretProtector secretProtector,
    ILogger<OidcTokenClient> logger) : IOidcTokenClient
{
    /// <summary>
    ///     Named HTTP client — shared with the discovery-document
    ///     fetch (<c>OrgAuthProviderControllerHelpers</c>) and the
    ///     JWKS fetcher (<see cref="JwksFetcher" />). All three
    ///     routes are outbound HTTPS to an external IDP, no auth
    ///     header at the transport layer, 10s timeout.
    /// </summary>
    private const string HttpClientName = "Plexor-OidcDiscovery";

    /// <summary>
    ///     Keycloak's standard token-endpoint path. v1 hardcodes
    ///     this for the Keycloak/Authentik/Dex shape; Auth0's
    ///     <c>/oauth/token</c> and Azure AD's <c>/oauth2/v2.0/token</c>
    ///     diverge. Phase 5+ reads <c>token_endpoint</c> from the
    ///     discovery document (already in the JwksFetcher cache).
    /// </summary>
    private const string KeycloakTokenEndpointPath = "/protocol/openid-connect/token";

    /// <inheritdoc />
    public async Task<OidcTokenResponse?> ExchangeCodeAsync(
        Guid orgId,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var config = await configReader.GetForOrgAsync(orgId, cancellationToken);
        if (config is null || config.Provider != OrgAuthProvider.Oidc)
        {
            logger.LogWarning(
                "OidcTokenClient: no OIDC config for org {OrgId}",
                orgId);
            return null;
        }

        if (string.IsNullOrEmpty(config.OidcAuthority)
            || string.IsNullOrEmpty(config.OidcClientId))
        {
            logger.LogWarning(
                "OidcTokenClient: OIDC config for org {OrgId} has missing authority or client id",
                orgId);
            return null;
        }

        var secret = secretProtector.Decrypt(config.OidcClientSecretProtected);
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogError(
                "OidcTokenClient: empty decrypted client secret for org {OrgId}; check the keyring",
                orgId);
            return null;
        }

        var http = httpClientFactory.CreateClient(HttpClientName);
        var tokenEndpoint = new Uri(new Uri(config.OidcAuthority), KeycloakTokenEndpointPath);

        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config.OidcClientId}:{secret}")));

        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = config.OidcClientId,
                ["code_verifier"] = codeVerifier,
            });

        var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OidcTokenClient: token exchange failed for org {OrgId} with {Status}",
                orgId,
                response.StatusCode);
            return null;
        }

        var body = await response.Content
            .ReadFromJsonAsync<OidcTokenResponseDto>(cancellationToken: cancellationToken);
        if (body is null)
        {
            logger.LogWarning(
                "OidcTokenClient: empty response body for org {OrgId}",
                orgId);
            return null;
        }

        if (string.IsNullOrEmpty(body.AccessToken))
        {
            logger.LogWarning(
                "OidcTokenClient: response for org {OrgId} has no access_token",
                orgId);
            return null;
        }

        return new OidcTokenResponse(
            AccessToken: body.AccessToken,
            IdToken: body.IdToken ?? string.Empty,
            TokenType: string.IsNullOrEmpty(body.TokenType) ? "Bearer" : body.TokenType,
            ExpiresIn: body.ExpiresIn,
            RefreshToken: body.RefreshToken);
    }

    /// <summary>
    ///     RFC 6749 §5.1 JSON shape. <see cref="AccessToken" /> is
    ///     the only mandatory field per spec; <see cref="IdToken" />
    ///     comes from OIDC Core 1.0 §3.1.3.3 and is optional when
    ///     the caller requested only <c>openid</c> scopes (rare for
    ///     the authorization-code flow but tolerated by the client).
    /// </summary>
    /// <param name="AccessToken">RFC 6749 §5.1.</param>
    /// <param name="IdToken">OIDC Core 1.0 §3.1.3.3 (nullable).</param>
    /// <param name="TokenType">RFC 6749 §5.1 — always
    /// <c>"Bearer"</c> for the OIDC flow.</param>
    /// <param name="ExpiresIn">RFC 6749 §5.1 — seconds until the
    /// access token expires.</param>
    /// <param name="RefreshToken">RFC 6749 §6 — issued only when
    /// <c>offline_access</c> scope was requested.</param>
    private sealed record OidcTokenResponseDto(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
