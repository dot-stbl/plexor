// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IPermissionResolver — resolves the effective permission set for an
// authenticated user. Reads role_bindings + roles from IdentityDbContext
// and unions the bound roles' permissions into a single list. Used at
// sign-in time to bake the resolved permissions into the access token's
// claims (avoids a per-request DB roundtrip on every auth check).
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Reads the effective permission set for a user within an organization.
///     The implementation walks the role_bindings → roles graph and returns
///     the union of permissions across all bound roles. Built-in roles
///     (BuiltIn = true) ship with their permissions hard-coded in the
///     domain catalog; custom (BuiltIn = false) roles pull from the DB.
/// </summary>
/// <remarks>
///     <para><b>Why a service and not a property of the principal.</b>
///     A <see cref="System.Security.Claims.ClaimsPrincipal" /> is built
///     once per request from the bearer handler. Permission resolution
///     needs the DB and is a one-shot cost at sign-in — caching the
///     resolved set as claims avoids the same query on every
///     authorization check.</para>
///     <para><b>No cache layer yet.</b> The implementation reads from the
///     DB on every call. For Phase 4 traffic this is fine (sign-in is
///     cheap; refresh is the hot path but still rare compared to
///     authorize-checks-per-request). Phase 5+ adds a per-user
///     permission cache with TTL.</para>
/// </remarks>
public interface IPermissionResolver
{
    /// <summary>
    ///     Resolve the effective permission strings for a user. The
    ///     returned set is the union of every bound role's permissions;
    ///     duplicates are removed and the order is unspecified.
    /// </summary>
    /// <param name="userId">The user's identity (sigil.users.id).</param>
    /// <param name="orgId">Tenant scope (sigil.users.org_id).</param>
    /// <param name="cancellationToken">Forwarded to the DB query.</param>
    /// <returns>
    ///     A read-only collection of permission strings (lowercase, no
    ///     duplicates). Empty when the user has no role bindings.
    /// </returns>
    public Task<IReadOnlyCollection<string>> ResolveAsync(
        Guid userId,
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Compute the union of permissions for a caller-supplied
    ///     role-name list within a tenant. v0.1 trust boundary: the
    ///     caller already filtered the names through
    ///     <see cref="IRoleResolver.RolesByNamesForOrgAsync" /> (or
    ///     otherwise established the names map to existing
    ///     <c>Role</c> rows). Names that don't match a Plexor role
    ///     in the tenant are silently dropped.
    /// </summary>
    /// <param name="orgId">Tenant scope (sigil.roles.org_id /
    ///     realm.organizations.id).</param>
    /// <param name="roleNames">Role-name strings to project into
    ///     permissions. Empty / null entries are filtered out.</param>
    /// <param name="cancellationToken">Forwarded to the DB query.</param>
    /// <returns>
    ///     A read-only collection of permission strings (lowercase, no
    ///     duplicates). Empty when no supplied name matches a Plexor
    ///     role, or when no role carries permissions.
    /// </returns>
    /// <remarks>
    ///     <para><b>Why a name-based projection.</b> Mirrors
    ///     <see cref="IRoleResolver.RolesByNamesForOrgAsync" />:
    ///     the OIDC path doesn't go through <c>role_bindings</c>.
    ///     Instead of user → role_bindings → roles → permissions,
    ///     the OIDC provider already holds the role-name list
    ///     (from the IDP claims) and just needs the union of
    ///     permissions those names carry. Single DB roundtrip with
    ///     <c>WHERE role.Name IN (...)</c> + select-many.</para>
    /// </remarks>
    public Task<IReadOnlyCollection<string>> PermissionsForRolesAsync(
        Guid orgId,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default);
}
