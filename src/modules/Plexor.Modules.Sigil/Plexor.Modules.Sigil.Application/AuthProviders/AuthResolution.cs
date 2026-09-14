// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthResolution — outcome of IAuthProvider.ResolveAsync. Carries
// everything the future bearer dispatcher (4.6.2c) needs to populate
// HttpContext.User + the per-request ICurrentUser after a successful
// provider resolution.
//
// Today the bearer handler does this work itself from the JWT claims
// (see BearerAuthenticationHandler.VerifyJwtAsync); the AuthResolution
// shape exists so the dispatcher can build the same principal from
// either a Sigil-issued token (this commit) or an OIDC-issued token
// (4.6.2b) without knowing which provider produced it.
// ============================================================================

using Plexor.Modules.Sigil.Application.Abstractions;

namespace Plexor.Modules.Sigil.Application.AuthProviders;

/// <summary>
///     One resolved principal. The future bearer dispatcher builds an
///     authentication ticket from this — claims are minted against the
///     same <see cref="IdentityClaims" /> constants the per-request
///     <see cref="ICurrentUser" /> reads back, so the downstream
///     <c>ICurrentUser</c> sees no difference between a Sigil-issued
///     token and an OIDC-issued one.
/// </summary>
/// <param name="OrgId">
///     Tenant scope (sigil.users.org_id / realm.organizations.id).
/// </param>
/// <param name="UserId">
///     Plexor-side user id. For Sigil-issued tokens this is the
///     JWT <c>sub</c> claim verbatim; for OIDC-issued tokens (4.6.2b)
///     it's a deterministic GUID v5 derived from the OIDC issuer + the
///     external <c>sub</c> claim, so the same external user maps to
///     the same Plexor user id across logins.
/// </param>
/// <param name="ProviderId">
///     Which <see cref="IAuthProvider" /> produced this resolution.
///     Forwarded to <c>ICurrentUser</c> for audit logging; not used
///     for routing decisions (those happen at the dispatcher).
/// </param>
/// <param name="IsService">
///     Always <c>false</c> in v0.1 — service-to-service auth flows
///     through API keys (handled separately by
///     <see cref="Plexor.Modules.Sigil.Application.Auth.IApiKeyAuthenticationService" />),
///     never through an <see cref="IAuthProvider" />. The field is on
///     the record for forward compatibility with future provider
///     implementations that might mint machine identities (a Plexor
///     mTLS provider, for instance).
/// </param>
/// <param name="Roles">
///     Role names bound to the principal. Mirrors the
///     <c>role</c> claims the Sigil token issuer bakes into access
///     tokens today; the dispatcher mints the same claims from this
///     collection so
///     <see cref="ICurrentUser.Roles" /> reads them back identically.
/// </param>
/// <param name="Permissions">
///     Effective permissions (union of role permissions + per-user
///     overrides). Same forward shape as
///     <see cref="ICurrentUser.Permissions" />; the dispatcher mints
///     <c>permission</c> claims from this collection. Empty when the
///     user has no role bindings.
/// </param>
/// <param name="TokenLifetime">
///     TimeSpan until the underlying token expires. v0.1 ships the
///     constant
///     <see cref="Plexor.Modules.Sigil.Application.Auth.IJwtSigningService.AccessTokenLifetime" />
///     (15 min). The future dispatcher uses this for sliding-session
///     bookkeeping + refresh-token rotation tied to provider lifetime
///     (OIDC refresh-token TTL varies per IDP).
/// </param>
public sealed record AuthResolution(
    Guid OrgId,
    Guid UserId,
    AuthProviderId ProviderId,
    bool IsService,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    TimeSpan TokenLifetime);
