// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOidcTokenClient — outbound token-exchange seam for the OIDC
// authorization-code flow (RFC 6749 §4.1.3). Phase 4.6.3a.
//
// Called by the OIDC callback endpoint (4.6.3b) on the second leg
// of the authorize → callback round-trip. The call:
//   1. Looks up the tenant's OrgAuthProviderConfig.
//   2. Decrypts the per-tenant OIDC client secret.
//   3. POSTs to {authority}/protocol/openid-connect/token with:
//        - grant_type=authorization_code
//        - code=<callback code>
//        - redirect_uri=<the same URI used on the authorize leg>
//        - client_id=<oidcClientId>
//        - code_verifier=<PKCE verifier>
//      ...authenticating with HTTP Basic on the
//      clientId:clientSecret pair.
//   4. Returns the access_token + id_token + token_type + expires_in
//      + refresh_token bundle to the caller.
//
// v1 hardcodes the Keycloak-style path
// {authority}/protocol/openid-connect/token. Phase 5+ reads the
// token_endpoint from the discovery document cached by JwksFetcher.
// ============================================================================

namespace Plexor.Shared.Kernel.AuthProviders;

/// <summary>
///     Outbound token-exchange client for the OIDC authorization-
///     code flow (RFC 6749 §4.1.3). Calls the per-tenant IDP's
///     token endpoint using the per-tenant decrypted client
///     secret for HTTP Basic auth.
/// </summary>
public interface IOidcTokenClient
{
    /// <summary>
    ///     Exchange an authorization code for tokens at the IDP's
    ///     token endpoint (RFC 6749 §4.1.3). The call uses the
    ///     tenant's decrypted client secret for HTTP Basic auth on
    ///     the outbound request.
    /// </summary>
    /// <remarks>
    ///     <para>Returns <c>null</c> on every failure path:</para>
    ///     <list type="bullet">
    ///         <item>no <c>OrgAuthProviderConfig</c> row for the
    ///         org, or the row's <c>Provider</c> is
    ///         <c>OrgAuthProvider.Sigil</c> (not Oidc);</item>
    ///         <item>decrypted client secret is empty (org admin
    ///         hasn't supplied one yet);</item>
    ///         <item>transport failure (network / 4xx / 5xx /
    ///         timeout);</item>
    ///         <item>response body is malformed JSON or has no
    ///         <c>access_token</c> field.</item>
    ///     </list>
    ///     <para>The OIDC callback endpoint (4.6.3b) maps <c>null</c>
    ///     to a 502 (upstream failure) on the wire; the structured
    ///     log line carries the discriminator (config vs secret vs
    ///     transport) for diagnostics.</para>
    /// </remarks>
    /// <param name="orgId">Tenant id (the inbound identity has
    /// already been resolved to an org before this call).</param>
    /// <param name="code">Authorization code returned by the IDP
    /// in the callback redirect's <c>code</c> query parameter.</param>
    /// <param name="codeVerifier">PKCE code_verifier minted on the
    /// authorize leg and persisted server-side until the callback
    /// returns (RFC 7636 §4.6).</param>
    /// <param name="redirectUri">Redirect URI registered at the
    /// IDP — must match the authorize leg's <c>redirect_uri</c>
    /// exactly (RFC 6749 §4.1.3, §10.6).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The token bundle on success; <c>null</c> on any
    /// failure path.</returns>
    public Task<OidcTokenResponse?> ExchangeCodeAsync(
        Guid orgId,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Token bundle returned by <see cref="IOidcTokenClient.ExchangeCodeAsync" />.
///     Mirrors the JSON shape of RFC 6749 §5.1.
/// </summary>
/// <param name="AccessToken">The bearer token the IDP issued for
/// the user. Sent on every Plexor API call as
/// <c>Authorization: Bearer &lt;accessToken&gt;</c>.</param>
/// <param name="IdToken">The ID token (JWT) — claims (sub, iss,
/// aud, ...) drive the inbound validation path (Phase 4.6.2b). May
/// be empty if the IDP didn't return one (some providers omit
/// id_token on refresh-token exchanges; this is the authorization-
/// code path, so the empty case is rare but tolerated).</param>
/// <param name="TokenType">RFC 6749 §5.1 — always <c>"Bearer"</c>
/// for the OIDC flow; defaulted to <c>"Bearer"</c> when the IDP
/// omits the field.</param>
/// <param name="ExpiresIn">Seconds until the access token expires.
/// Future dispatcher (4.6.3c) uses this for the
/// <c>AuthResolution.TokenLifetime</c> sliding-session
/// bookkeeping.</param>
/// <param name="RefreshToken">Optional refresh token (RFC 6749 §6);
/// the IDP only issues one when the client is configured with
/// <c>offline_access</c> in the requested scopes. Phase 4.6.3c
/// re-mints Plexor access tokens from this when the inbound
/// access token expires.</param>
public sealed record OidcTokenResponse(
    string AccessToken,
    string IdToken,
    string TokenType,
    int ExpiresIn,
    string? RefreshToken);
