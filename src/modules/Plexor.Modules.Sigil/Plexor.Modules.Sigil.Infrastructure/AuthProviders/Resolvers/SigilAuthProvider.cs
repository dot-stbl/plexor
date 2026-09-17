// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigilAuthProvider — IAuthProvider implementation for Plexor's local
// email+password backend. Resolves a Sigil-issued compact JWT into an
// AuthResolution by:
//
//   1. Verifying the JWT signature + lifetime via IJwtSigningService.
//   2. Confirming the tenant's OrgAuthProviderConfig is still Sigil
//      (a token issued against a Sigil-configured tenant must not be
//      honoured after the admin flips the row to Oidc) — read via
//      IOrgAuthProviderConfigReader.
//   3. Loading the user from the verified subject id and confirming
//      Status == "active".
//   4. Resolving roles + permissions from the live role_bindings +
//      roles graph (NOT from the JWT claims — the Sigil provider is
//      the source of truth, the JWT carries a snapshot).
//
// The bearer handler today bypasses this provider (it has its own
// VerifyJwtAsync + claim-building path). The future dispatcher
// (Phase 4.6.2c) will route the bearer credential through this
// provider; the existing bearer path remains in place until then.
// ============================================================================

using Microsoft.Extensions.Logging;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Application.Users;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers;

/// <summary>
///     <see cref="IAuthProvider" /> implementation for the Sigil
///     (local email + password) backend. Reads the Sigil JWT, the
///     user row, the tenant's <c>OrgAuthProviderConfig</c>, and the
///     live role + permission sets; assembles an
///     <see cref="AuthResolution" /> the future dispatcher uses to
///     rebuild <c>HttpContext.User</c>.
/// </summary>
/// <remarks>
///     <para><b>Cross-module seam.</b> Reads the per-tenant config
///     via <see cref="IOrgAuthProviderConfigReader" /> — the
///     abstraction defined in
///     <c>Plexor.Modules.Realm.Application.AuthProviders</c>. This
///     class never touches <c>RealmDbContext</c> directly (Law 3:
///     modules don't reference each other's Infrastructure).</para>
///     <para><b>No <c>private</c> helpers.</b> Per project convention
///     <c>code-shape.md §9</c>, every step lives inline or behind a
///     separate service. Role resolution lives in
///     <see cref="IRoleResolver" />; permission resolution in
///     <see cref="IPermissionResolver" />. The
///     <see cref="OrgAuthProviderConfig" /> lookup is a single reader
///     call — no need to extract.</para>
///     <para><b>Why re-resolve roles + permissions on every call.</b>
///     The Sigil provider is the source of truth for which
///     permissions a user holds at this instant. The JWT carries a
///     snapshot from issue time; role-binding changes since issue
///     would not be visible otherwise. Cost: 2 small roundtrips on
///     every authenticated request (cached at the dispatcher once
///     4.6.2c lands). Acceptable for v0.1; Phase 5+ adds a TTL cache.</para>
/// </remarks>
/// <param name="signingService">Verifies the JWT signature + claims
///     + revocation. The signature-valid output carries the principal
///     whose <c>sub</c> and <c>tid</c> claims drive the user lookup.</param>
/// <param name="userLookup">Loads the resolved subject (the JWT's
///     <c>sub</c> claim) for status + presence checks.</param>
/// <param name="roleResolver">Reads the role-name set bound to the
///     resolved user. Mirrors <see cref="IRoleResolver" />.</param>
/// <param name="permissionResolver">Reads the union of permissions
///     bound to the resolved user via their roles.</param>
/// <param name="configReader">Reads the per-tenant
///     <see cref="OrgAuthProviderConfig" /> via the cross-module
///     abstraction.</param>
/// <param name="logger">Logs the tenant-mismatch case at warning level
///     (security-relevant event).</param>
public sealed class SigilAuthProvider(
    IJwtSigningService signingService,
    IUserLookup userLookup,
    IRoleResolver roleResolver,
    IPermissionResolver permissionResolver,
    IOrgAuthProviderConfigReader configReader,
    ILogger<SigilAuthProvider> logger) : IAuthProvider
{
    /// <inheritdoc />
    public AuthProviderId ProviderId => AuthProviderId.Sigil;

    /// <inheritdoc />
    public async Task<bool> CanAuthenticateForAsync(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        var config = await configReader.GetForOrgAsync(orgId, cancellationToken);

        // v0.1 single-tenant boot state: an org without a config row
        // is treated as Sigil-served (the OrgAuthProviderSeeder
        // backfills on first boot; an in-flight first request can
        // observe an absent row). Once 4.6.1 is rolled out, every
        // tenant has the row within one Migrator pass.
        return config is null or { Provider: OrgAuthProvider.Sigil };
    }

    /// <inheritdoc />
    public async Task<AuthResolution?> ResolveAsync(
        string rawCredential,
        CancellationToken cancellationToken)
    {
        var verification = await signingService.VerifyAsync(
            rawCredential, cancellationToken);

        if (verification is not VerifyResult.Success success)
        {
            // Malformed / invalid / expired — treat as "this provider
            // doesn't claim this credential". The dispatcher (4.6.2c)
            // sees null and either tries the next provider or emits 401.
            return null;
        }

        var principal = success.Principal;
        var userIdRaw = principal.FindFirst(IdentityClaims.UserId)?.Value;
        var orgIdRaw = principal.FindFirst(IdentityClaims.TenantId)?.Value;

        if (!Guid.TryParse(userIdRaw, out var userId)
            || !Guid.TryParse(orgIdRaw, out var orgId))
        {
            return null;
        }

        if (!await CanAuthenticateForAsync(orgId, cancellationToken))
        {
            logger.LogWarning(
                "Sigil provider rejected token for org {OrgId}: tenant is no longer Sigil-served",
                orgId);
            return null;
        }

        var user = await userLookup.FindByIdAsync(userId, cancellationToken);
        if (user is null || !string.Equals(user.Status, "active", StringComparison.Ordinal))
        {
            return null;
        }

        var roles = await roleResolver.ResolveAsync(
            userId, orgId, cancellationToken);
        var permissions = await permissionResolver.ResolveAsync(
            userId, orgId, cancellationToken);

        return new AuthResolution(
            OrgId: orgId,
            UserId: userId,
            ProviderId: AuthProviderId.Sigil,
            IsService: false,
            Roles: roles,
            Permissions: permissions,
            TokenLifetime: IJwtSigningService.AccessTokenLifetime);
    }
}
