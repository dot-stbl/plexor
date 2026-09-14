// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IRoleResolver — resolves the role names bound to a user within an
// organization. Mirrors IPermissionResolver (Phase 3.7) but returns
// the role-name set (used to bake the `role` claims into the access
// token) instead of the union of permissions.
//
// Used by SigilAuthProvider (Phase 4.6.2a) to assemble the
// AuthResolution the future dispatcher (4.6.2c) needs to rebuild the
// ClaimsPrincipal on every request. The implementation walks
// role_bindings → roles and projects to role names — same shape as
// the existing private LoadRolesAsync on AuthCommandHandlers, lifted
// to its own abstraction so the future dispatcher can swap in an
// OIDC-flavored implementation without touching the Sigil provider.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Reads the effective role-name set for a user within an
///     organization. The implementation walks the role_bindings →
///     roles graph and returns the deduplicated set of role names
///     bound to the user. Mirrors <see cref="IPermissionResolver" />
///     but projects to <c>role.Name</c> instead of the union of
///     permissions.
/// </summary>
/// <remarks>
///     <para><b>Why a separate resolver.</b> Roles are exposed to
///     ASP.NET Core's authorization pipeline via the
///     <c>role</c> claim; permissions are gated by Plexor's
///     <c>RequirePermission</c> attribute. Two claim families, two
///     DB queries, two services — keeps each one a single roundtrip
///     without a join + select-many.</para>
///     <para><b>No cache layer yet.</b> The implementation reads from
///     the DB on every call. For Phase 4 traffic this is fine —
///     refresh is the hot path and still rare compared to authorize-
///     checks-per-request. Phase 5+ adds a per-user role + permission
///     cache with TTL.</para>
/// </remarks>
public interface IRoleResolver
{
    /// <summary>
    ///     Resolve the role names bound to a user within an
    ///     organization. The returned set is the deduplicated list of
    ///     <c>role.Name</c> values across every active role binding;
    ///     order is unspecified.
    /// </summary>
    /// <param name="userId">The user's identity (sigil.users.id).</param>
    /// <param name="orgId">Tenant scope (sigil.users.org_id).</param>
    /// <param name="cancellationToken">Forwarded to the DB query.</param>
    /// <returns>
    ///     A read-only collection of role-name strings (lowercase, no
    ///     duplicates). Empty when the user has no role bindings.
    /// </returns>
    public Task<IReadOnlyCollection<string>> ResolveAsync(
        Guid userId,
        Guid orgId,
        CancellationToken cancellationToken = default);
}
