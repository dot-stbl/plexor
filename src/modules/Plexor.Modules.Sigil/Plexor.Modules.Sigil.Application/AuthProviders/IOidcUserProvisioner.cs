// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOidcUserProvisioner — find-or-create the Plexor User row + a
// default RoleBinding for a freshly-onboarded OIDC user. Phase 4.6.3c.
//
// Lives in Application because the callback endpoint (Api layer) needs
// to invoke it; the EF implementation in Infrastructure depends on the
// identity DbContext. The handler is the Application seam that keeps
// the endpoint thin (just validate id_token + call provisioner + mint
// Plexor bearer + redirect).
//
// The provisioner is NOT responsible for:
//   - Validating the id_token JWT (that's IOidcIdTokenValidator).
//   - Minting the Plexor bearer (that's ITokenIssuer — invoked by the
//     endpoint after ProvisionAsync returns).
//   - Reading the OrgAuthProviderConfig (the caller passes the
//     <see cref="OidcTenantConfig" /> projection).
//
// Atomicity: User + RoleBinding are inserted inside a single EF
// transaction so a half-provisioned user (User row but no role
// binding) is impossible. A failed insert rolls back; the caller
// surfaces the failure as 500.
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Application.AuthProviders;

/// <summary>
///     Find-or-create the Plexor <see cref="User" /> row + a default
///     <c>viewer</c>-role <see cref="RoleBinding" /> for an OIDC user
///     arriving via the authorization-code callback. The Plexor user
///     id is derived deterministically from <c>(iss, sub)</c> so the
///     same external user always maps to the same Plexor id across
///     logins (and across tenants — the id is global, not org-scoped).
/// </summary>
public interface IOidcUserProvisioner
{
    /// <summary>
    ///     Find-or-create the Plexor <see cref="User" /> row for
    ///     the supplied OIDC identity.
    /// </summary>
    /// <param name="config">Per-tenant OIDC configuration
    /// projection (authority URL + tenant id).</param>
    /// <param name="subject">OIDC <c>sub</c> claim (stable per
    /// issuer + user).</param>
    /// <param name="email">OIDC <c>email</c> claim. Required —
    /// missing value throws <c>oidc.user.missing_claims</c>.</param>
    /// <param name="preferredUsername">OIDC <c>preferred_username</c>
    /// claim. Primary display-name source.</param>
    /// <param name="name">OIDC <c>name</c> claim. Secondary
    /// display-name source — used when <paramref name="preferredUsername" />
    /// is missing.</param>
    /// <param name="cancellationToken">Forwarded to the DB.</param>
    /// <returns>The (existing or freshly-created) Plexor user.</returns>
    /// <exception cref="Domain.Errors.IdentityException">
    ///     Thrown when <paramref name="email" /> is missing
    ///     (<c>oidc.user.missing_claims</c>) or when the underlying
    ///     DB write fails (<c>oidc.user.provisioning_failed</c>).
    /// </exception>
    public Task<User> ProvisionAsync(
        OidcTenantConfig config,
        string subject,
        string email,
        string? preferredUsername,
        string? name,
        CancellationToken cancellationToken = default);
}
