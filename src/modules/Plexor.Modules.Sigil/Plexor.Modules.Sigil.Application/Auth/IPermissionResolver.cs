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
}
