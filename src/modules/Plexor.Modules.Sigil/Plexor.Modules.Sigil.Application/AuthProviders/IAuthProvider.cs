// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IAuthProvider — the abstraction the future auth dispatcher (4.6.2c)
// routes through. Each implementation is responsible for resolving a
// raw bearer credential into an AuthResolution for the tenant whose
// OrgAuthProviderConfig matches.
//
// v0.1 ships two implementations:
//   - SigilAuthProvider        (4.6.2a — this commit)
//   - ExternalOidcAuthProvider (4.6.2b — JWKS + per-tenant validator)
//
// Both live in Plexor.Modules.Sigil because they consume the Sigil
// module's signing/permission primitives; the OrgAuthProviderConfig row
// is read out of the realm module via a cross-module reference that's
// necessary for the tenant-routing decision (architecture-test backlog
// to gate Sigil → Realm.Infrastructure explicitly).
// ============================================================================

namespace Plexor.Modules.Sigil.Application.AuthProviders;

/// <summary>
///     One authentication provider. Resolves a raw bearer credential
///     into an <see cref="AuthResolution" /> for the tenant whose
///     <c>OrgAuthProviderConfig</c> matches this provider. The bearer
///     handler (Phase 4.6.2c) routes by the token's <c>iss</c> claim
///     to the matching provider; today it bypasses the resolver and
///     decodes Sigil JWTs directly.
/// </summary>
/// <remarks>
///     <para><b>Why nullable <see cref="AuthResolution" />.</b>
///     <see cref="ResolveAsync" /> returns <c>null</c> when the
///     credential doesn't belong to this provider (wrong shape,
///     tenant has switched to a different IDP, signature invalid,
///     subject deleted). Distinguishes "this provider didn't claim
///     this credential" from "this provider authenticated and the
///     caller is denied" — the latter is a 4xx the dispatcher emits;
///     the former is "try the next provider or 401".</para>
///     <para><b>Why no synchronous throw.</b>
///     <see cref="ResolveAsync" /> does not throw on auth failure —
///     it returns <c>null</c>. <c>HttpContext.User</c> stays empty and
///     the framework's 401 challenge handles it. The dispatcher
///     (4.6.2c) is the boundary that decides whether null means
///     "another provider might know" vs "401 now".</para>
/// <para><b>Scoped lifetime.</b> <see cref="IAuthProvider" /> is
///     scoped — its dependencies (the JWT signing service, the
///     user lookup, the realm DbContext) all share the per-request
///     scope.</para>
/// </remarks>
public interface IAuthProvider
{
    /// <summary>
    ///     Stable wire identifier. Matches the
    ///     <c>OrgAuthProviderConfig.Provider</c> discriminator so the
    ///     future dispatcher can pick the right implementation per
    ///     tenant config row.
    /// </summary>
    public AuthProviderId ProviderId { get; }

    /// <summary>
    ///     Returns <c>true</c> when this provider can serve the given
    ///     tenant. Reads <c>OrgAuthProviderConfig</c>; absent row +
    ///     <see cref="AuthProviderId.Sigil" /> → <c>true</c> (single-
    ///     tenant v0.1 default — the seeder backfills the row on first
    ///     boot, but in-flight races during the first request could
    ///     observe an absent config).
    /// </summary>
    /// <param name="orgId">Tenant id (sigil.users.org_id /
    ///     realm.organizations.id).</param>
    /// <param name="cancellationToken">Forwarded to the config read.</param>
    public Task<bool> CanAuthenticateForAsync(
        Guid orgId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Resolve a raw bearer credential into an
    ///     <see cref="AuthResolution" />. Returns <c>null</c> when this
    ///     provider cannot authenticate the credential — the
    ///     dispatcher routes the next provider or emits 401.
    /// </summary>
    /// <param name="rawCredential">Compact JWT string for the Sigil
    ///     provider; ID-token JWT string for the OIDC provider (4.6.2b).
    ///     The header parsing + scheme-routing is the dispatcher's job,
    ///     not this provider's.</param>
    /// <param name="cancellationToken">Forwarded to all sub-lookups.</param>
    public Task<AuthResolution?> ResolveAsync(
        string rawCredential,
        CancellationToken cancellationToken);
}
