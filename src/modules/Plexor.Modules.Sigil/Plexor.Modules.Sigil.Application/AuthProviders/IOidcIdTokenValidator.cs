// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOidcIdTokenValidator — validates the id_token returned by an
// external OIDC token endpoint. Phase 4.6.3c.
//
// The callback endpoint exchanges the authorization code for tokens
// via IOidcTokenClient (Phase 4.6.3a), receives an id_token JWT in
// the response, and hands that string here for signature + claim +
// lifetime validation. Returns a flat OidcIdTokenClaims carrier or
// null on validation failure (signature mismatch, expired, wrong
// audience, unknown kid, etc.).
//
// Why a separate seam from the bearer-side ExternalOidcAuthProvider
// (Phase 4.6.2b): the bearer provider validates every inbound HTTP
// request (hot path, cached JWKS); the callback validator runs once
// per authorization-code roundtrip (cold path, fresh JWKS fetch).
// Same algorithm, different call cadence. Sharing the underlying
// validation helpers via file-static methods avoids the duplication
// without coupling the two providers at the type level.
//
// Why the validator takes an <see cref="OidcTenantConfig" /> DTO
// rather than the raw <c>OrgAuthProviderConfig</c> entity: the
// Application layer does not (and must not) reference the Realm
// module. The endpoint resolves the entity, projects the four
// fields the validator needs, and hands the projection here.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.AuthProviders;

/// <summary>
///     Validates the <c>id_token</c> returned by the OIDC token
///     endpoint. Caller hands the raw JWT string + the
///     per-tenant <see cref="OidcTenantConfig" /> projection and
///     receives a typed claims carrier on success.
/// </summary>
public interface IOidcIdTokenValidator
{
    /// <summary>
    ///     Validate an <c>id_token</c> against the per-tenant
    ///     OIDC config. Performs signature + issuer + audience +
    ///     lifetime validation against the JWKS for the configured
    ///     authority. Returns <c>null</c> on any validation
    ///     failure (signature mismatch, expired, wrong audience,
    ///     unknown kid, missing <c>sub</c>, etc.) — the caller
    ///     surfaces 400 with the failure code; the inner reason is
    ///     logged but never returned to the wire (PII).
    /// </summary>
    /// <param name="idToken">The compact <c>id_token</c> JWT
    /// returned by the token endpoint.</param>
    /// <param name="config">Per-tenant OIDC configuration
    /// projection (authority URL + client id + tenant id).</param>
    /// <param name="cancellationToken">Forwarded to the JWKS
    /// fetch.</param>
    /// <returns>
    ///     A populated <see cref="OidcIdTokenClaims" /> carrier, or
    ///     <c>null</c> on validation failure.
    /// </returns>
    public Task<OidcIdTokenClaims?> ValidateAsync(
        string idToken,
        OidcTenantConfig config,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Structured carrier for the claims the callback handler
///     needs to provision a Plexor User row. Returned by
///     <see cref="IOidcIdTokenValidator.ValidateAsync" /> when
///     validation succeeds; one of <see cref="Email" /> or
///     <see cref="PreferredUsername" /> may be missing — the
///     provisioner handles the fallback.
/// </summary>
/// <param name="Issuer">OIDC <c>iss</c> claim (already
/// validated).</param>
/// <param name="Subject">OIDC <c>sub</c> claim (stable per
/// issuer + user).</param>
/// <param name="Email">OIDC <c>email</c> claim. May be missing —
/// the provisioner throws on null.</param>
/// <param name="PreferredUsername">OIDC <c>preferred_username</c>
/// claim. Optional — falls back to <see cref="Name" />.</param>
/// <param name="Name">OIDC <c>name</c> claim. Optional — final
/// fallback before the email local-part.</param>
public sealed record OidcIdTokenClaims(
    string Issuer,
    string Subject,
    string? Email,
    string? PreferredUsername,
    string? Name);

/// <summary>
///     Per-tenant OIDC configuration projection handed to the
///     validator + provisioner. The Application layer does not
///     reference the Realm module — the endpoint projects the
///     four fields it needs (OrgId, Authority, ClientId) into
///     this DTO and passes it through.
/// </summary>
/// <param name="OrgId">Tenant id (sigil.users.org_id /
/// realm.organizations.id).</param>
/// <param name="Authority">OIDC issuer URL — matched verbatim
/// against the JWT's <c>iss</c> claim.</param>
/// <param name="ClientId">OIDC client id — used as the
/// expected <c>aud</c> claim.</param>
public sealed record OidcTenantConfig(
    Guid OrgId,
    string Authority,
    string ClientId);
