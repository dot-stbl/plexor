// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOrgAuthProviderConfigReader — read-only seam over OrgAuthProviderConfig
// for callers outside the Realm module (Phase 4.6.3a). The OIDC token
// client (Plexor.Modules.Sigil.Infrastructure.AuthProviders.OidcTokenClient)
// resolves the per-tenant config without depending on RealmDbContext
// directly; the controller (Phase 4.6.1) keeps its direct DbContext
// dependency for write paths because they need ExecuteUpdate against the
// org row + concurrency on the secret column.
//
// Lifetime: Scoped (mirrors RealmDbContext — reads share the per-request
// scope).
// ============================================================================

using Plexor.Modules.Realm.Domain.Entities;

namespace Plexor.Modules.Realm.Application.AuthProviders;

/// <summary>
///     Read-only access to <see cref="OrgAuthProviderConfig" /> for
///     callers outside the Realm module. Two lookup shapes cover the
///     call sites in the auth-provider pipeline:
///     <list type="bullet">
///       <item>
///         <see cref="GetForOrgAsync" /> — used by the OIDC token
///         client (4.6.3a) once the inbound flow has resolved the org
///         from the caller's authenticated identity.
///       </item>
///       <item>
///         <see cref="GetByOidcIssuerAsync" /> — used by the inbound
///         JWT validation path (Phase 4.6.2b) to map an inbound
///         token's <c>iss</c> claim back to the org that owns the
///         IDP.
///       </item>
///     </list>
/// </summary>
public interface IOrgAuthProviderConfigReader
{
    /// <summary>
    ///     Fetch the <see cref="OrgAuthProviderConfig" /> row for a
    ///     given organization id. Returns <c>null</c> when no row
    ///     exists — the seeder normally backfills on first boot, but
    ///     a fresh DB without the seeder having run yet returns null
    ///     here and the caller decides what to do (the token client
    ///     logs + returns null).
    /// </summary>
    /// <param name="orgId">Organization id.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<OrgAuthProviderConfig?> GetForOrgAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Look up the <see cref="OrgAuthProviderConfig" /> row whose
    ///     <c>oidc_authority</c> matches the supplied OIDC issuer
    ///     URL. Used by the inbound JWT validation path (Phase
    ///     4.6.2b) to map an inbound token's <c>iss</c> claim back
    ///     to the org that owns the IDP. Returns <c>null</c> when no
    ///     tenant has configured an IDP at that URL — the inbound
    ///     validation path treats that as "this token doesn't belong
    ///     to any tenant we serve".
    /// </summary>
    /// <param name="oidcAuthority">OIDC issuer URL — matched
    /// verbatim against <see cref="OrgAuthProviderConfig.OidcAuthority" />.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<OrgAuthProviderConfig?> GetByOidcIssuerAsync(
        string oidcAuthority,
        CancellationToken cancellationToken = default);
}
