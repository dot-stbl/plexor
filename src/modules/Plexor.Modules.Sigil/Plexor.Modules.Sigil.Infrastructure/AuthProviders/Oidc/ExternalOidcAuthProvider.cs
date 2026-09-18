// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ExternalOidcAuthProvider — IAuthProvider implementation for the
// external OIDC backend. Validates an external IDP-issued JWT
// (the inbound ID-token, or an RFC 9068 access-token minted by
// the IDP) against the per-tenant OrgAuthProviderConfig:
//
//   1. Decode the JWT header (no signature check yet) to discover
//      `kid` + `iss`.
//   2. Look up the OrgAuthProviderConfig by issuer via
//      IOrgAuthProviderConfigReader — the dispatcher's
//      responsibility in 4.6.2c, but for now the provider self-routes
//      by issuer.
//   3. Fetch the JWKS via IJwksFetcher (cached 1h in-memory per
//      authority).
//   4. Validate the JWT signature + iss + aud + lifetime with the
//      matching signing key.
//   5. Extract the `sub` claim and derive a deterministic Plexor
//      user id (SHA-256 over (issuer|sub)) — same external user
//      → same Plexor user id across logins.
//   6. Extract external role names (Keycloak `realm_access.roles[]`
//      primary; generic `roles[]` fallback; Plexor-namespaced
//      `https://plexor.example.com/roles[]` ternary).
//   7. Project external role names through Plexor's `Role` table
//      for the tenant — v0.1 trust boundary: exact name match
//      (tenant operator configures IDP roles to match Plexor
//      `role.Name`). Phase 5+ adds a per-tenant role-mapping table.
//   8. Union the matched roles' permissions and return an
//      AuthResolution.
//
// The bearer handler today bypasses this provider; the future
// dispatcher (Phase 4.6.2c) routes the bearer credential through
// it. The dispatcher will need to inject both SigilAuthProvider
// and ExternalOidcAuthProvider via a typed factory or registry —
// 4.6.2b registers the OIDC concrete separately from the IAuthProvider
// binding (the existing one is SigilAuthProvider) to avoid ASP.NET's
// last-wins semantics on multi-IAuthProvider registrations.
// ============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;

/// <summary>
///     <see cref="IAuthProvider" /> implementation for the external
///     OIDC backend. Validates external JWTs against the per-tenant
///     <see cref="OrgAuthProviderConfig" /> (issuer + audience +
///     JWKS) and returns an <see cref="AuthResolution" /> the future
///     dispatcher (4.6.2c) uses to rebuild <c>HttpContext.User</c>.
/// </summary>
/// <remarks>
///     <para><b>Cross-module seam.</b> Reads the per-tenant config via
///     <see cref="IOrgAuthProviderConfigReader" /> — the abstraction
///     defined in <c>Plexor.Modules.Realm.Application.AuthProviders</c>.
///     This class never touches <c>RealmDbContext</c> directly (Law 3:
///     modules don't reference each other's Infrastructure).</para>
///     <para><b>Why <see cref="JsonWebTokenHandler" />.</b> Microsoft's
///     modern JWT pipeline (replaces <c>JwtSecurityTokenHandler</c>).
///     Reads + validates in one call via
///     <c>ValidateTokenAsync</c>; handles kid lookup against the
///     supplied <see cref="SecurityKey" />. The
///     <c>JsonWebToken.Claims</c> collection preserves the raw
///     claim names (<c>sub</c>, <c>iss</c>, ...) — Microsoft's
///     JwtSecurityTokenHandler does an inbound legacy mapping to
///     <c>ClaimTypes.*</c> that we don't want here.</para>
///     <para><b>Why re-resolve roles + permissions on every call.</b>
///     The OIDC provider is the source of truth for which Plexor
///     roles + permissions an external user holds. Two small
///     roundtrips on every authenticated request; the dispatcher
///     (4.6.2c) caches the result. Phase 5+ adds a TTL cache.</para>
///     <para><b>Secret decryption is deferred.</b> v1 JWT
///     validation doesn't need the OIDC client secret — signature
///     validation is public-key, not HMAC. The secret is used by
///     4.6.3 (OIDC flow endpoints) for client authentication at the
///     IDP's token endpoint. The decryption seam
///     (<see cref="IOrgAuthProviderSecretProtector" />, registered
///     via <see cref="Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers.AuthProviderResolver" />'s peer consumer
///     <c>OrgAuthProviderSecretProtector</c> in Realm) will land with
///     4.6.3 alongside the flow.</para>
///     <para><b>User lookup is deferred.</b> v1 JWT validation
///     doesn't read the Plexor <c>User</c> row — the OIDC user is
///     created by the 4.6.3 flow endpoints when the user lands.
///     <see cref="Application.Users.IUserLookup" /> lands in
///     4.6.3 alongside the user-provisioning flow.</para>
/// </remarks>
/// <param name="jwksFetcher">Resolves the JWKS for the configured
/// authority (cached 1h, fetched on miss).</param>
/// <param name="roleResolver">Projects external role names through
/// the Plexor <c>Role</c> table for the tenant.</param>
/// <param name="permissionResolver">Unions the matched roles'
/// permissions.</param>
/// <param name="configReader">Reads the per-tenant
/// <see cref="OrgAuthProviderConfig" /> via the cross-module
/// abstraction.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ExternalOidcAuthProvider(
    IJwksFetcher jwksFetcher,
    IRoleResolver roleResolver,
    IPermissionResolver permissionResolver,
    IOrgAuthProviderConfigReader configReader,
    ILogger<ExternalOidcAuthProvider> logger) : IAuthProvider
{
    /// <summary>
    ///     Default token lifetime advertised by the OIDC provider in
    ///     the returned <see cref="AuthResolution" />. The dispatcher
    ///     uses this for sliding-session bookkeeping. OIDC ID-tokens
    ///     typically carry a 1h lifetime; the actual value lives in
    ///     the JWT's <c>exp</c> claim and is exposed via
    ///     <c>AuthenticationProperties.ExpiresUtc</c> by the
    ///     dispatcher, not through this constant.
    /// </summary>
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromHours(1);

    /// <summary>
    ///     Tolerance window for <c>exp</c> / <c>nbf</c> checks
    ///     against the system clock. 1 minute is the standard OIDC
    ///     recommendation (RFC 7519 §4.1.4) — covers clock skew
    ///     between Plexor and the IDP without opening a wide replay
    ///     window.
    /// </summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public AuthProviderId ProviderId => AuthProviderId.Oidc;

    /// <inheritdoc />
    public async Task<bool> CanAuthenticateForAsync(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        var config = await configReader.GetForOrgAsync(orgId, cancellationToken);

        return config is { Provider: OrgAuthProvider.Oidc };
    }

    /// <inheritdoc />
    public async Task<AuthResolution?> ResolveAsync(
        string rawCredential,
        CancellationToken cancellationToken)
    {
        var (handler, token) = ExternalOidcAuthProviderHelpers.ReadToken(rawCredential);
        if (token is null)
        {
            return null;
        }

        var issuer = token.Issuer;
        var kid = token.Kid;

        if (string.IsNullOrEmpty(issuer))
        {
            logger.LogInformation(
                "ExternalOidcAuthProvider: inbound JWT has no iss claim.");
            return null;
        }

        var config = await configReader.GetByOidcIssuerAsync(issuer, cancellationToken);

        if (config is null)
        {
            logger.LogWarning(
                "ExternalOidcAuthProvider: no OrgAuthProviderConfig for issuer {Issuer}",
                issuer);
            return null;
        }

        if (string.IsNullOrEmpty(config.OidcClientId))
        {
            logger.LogWarning(
                "ExternalOidcAuthProvider: org {OrgId} has no OidcClientId configured",
                config.OrgId);
            return null;
        }

        // OrgAuthProviderConfig.OidcAuthority is nullable in the
        // entity (Sigil rows carry null); reach this branch only
        // when the row's Provider == Oidc, so the value is
        // expected non-null. The validator (Phase 4.6.1) enforces
        // non-null + absolute + HTTPS on PUT, so a null here
        // indicates torn state — log + return null rather than
        // proceed with an empty authority.
        var authority = config.OidcAuthority;
        if (string.IsNullOrEmpty(authority))
        {
            logger.LogWarning(
                "ExternalOidcAuthProvider: org {OrgId} has Provider=Oidc but no OidcAuthority",
                config.OrgId);
            return null;
        }

        // Defer JWKS fetch until we know the credential is shaped
        // right; a malformed JWT short-circuits before the HTTP
        // roundtrip. The kid resolution below tolerates a null kid
        // (Keycloak always sends one; some IDPs don't), but the
        // TokenValidationParameters require a single IssuerSigningKey
        // when ValidateIssuerSigningKey = true, so the kid lookup
        // gates the rest of the path.
        var keySet = await jwksFetcher.GetKeySetAsync(authority, cancellationToken);

        var signingKey = ExternalOidcAuthProviderHelpers.ResolveSigningKey(keySet, kid);
        if (signingKey is null)
        {
            logger.LogWarning(
                "ExternalOidcAuthProvider: kid {Kid} not in JWKS for {Authority}",
                kid,
                authority);
            return null;
        }

        var validation = await ExternalOidcAuthProviderHelpers.ValidateAsync(
            handler,
            rawCredential,
            authority,
            config.OidcClientId,
            signingKey,
            ClockSkew);

        if (!validation.IsValid)
        {
            logger.LogInformation(
                "ExternalOidcAuthProvider: token validation failed for {Issuer}: {Reason}",
                issuer,
                validation.Exception?.Message);
            return null;
        }

        // Token claims live on the validated security token; the
        // ClaimsIdentity returned by ValidateTokenAsync goes through
        // Microsoft's inbound claim-type mapping, so we re-read from
        // the parsed JsonWebToken for raw claim names (esp. `sub`).
        var sub = token.Claims
            .FirstOrDefault(claim => claim.Type == "sub")
            ?.Value;
        if (string.IsNullOrEmpty(sub))
        {
            logger.LogInformation(
                "ExternalOidcAuthProvider: validated token has no sub claim.");
            return null;
        }

        var userId = ExternalOidcAuthProviderHelpers.DeterministicUserId(issuer, sub);

        var externalRoles = ExternalOidcAuthProviderHelpers.ExtractExternalRoles(token.Claims);
        var plexorRoles = await roleResolver.RolesByNamesForOrgAsync(
            config.OrgId,
            externalRoles,
            cancellationToken);
        var permissions = await permissionResolver.PermissionsForRolesAsync(
            config.OrgId,
            plexorRoles,
            cancellationToken);

        return new AuthResolution(
            OrgId: config.OrgId,
            UserId: userId,
            ProviderId: AuthProviderId.Oidc,
            IsService: false,
            Roles: plexorRoles,
            Permissions: permissions,
            TokenLifetime: DefaultTokenLifetime);
    }
}
