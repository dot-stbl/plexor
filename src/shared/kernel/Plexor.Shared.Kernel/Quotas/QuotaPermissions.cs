// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaPermissions — stable permission strings for the quotas capability.
// Lives in Plexor.Shared.Kernel alongside QuotaExceededException /
// IQuotaEnforcer so [RequirePermission(...)] attributes and role-seed code
// can share the same string constants without crossing the kernel → module
// boundary.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Stable permission strings for the quotas capability. The wildcard
///     <see cref="AdminWildcard" /> covers every permission (including
///     quotas) for the built-in admin role; per-operation strings like
///     <see cref="Read" /> and <see cref="AssignOrg" /> are scoped to a
///     single action.
/// </summary>
/// <remarks>
///     <para><b>Used by.</b>
///     <list type="bullet">
///       <item><c>[RequirePermission(QuotaPermissions.Read)]</c> on every
///       read endpoint of <c> QuotasController </c> (4.5.g.2).</item>
///       <item>The Migrator's <c>IdentityBootstrapper</c> seeds
///       <c>quotas.read</c> into the built-in <c>viewer</c> role and
///       leaves the built-in <c>admin</c> role's <c>*</c> wildcard
///       alone (admin already covers every permission).</item>
///     </list></para>
/// </remarks>
public static class QuotaPermissions
{
    /// <summary>
    ///     View definitions, assignments, usage, and effective values
    ///     (<c>GET /api/v1/quotas/*</c> read endpoints).
    /// </summary>
    public const string Read = "quotas.read";

    /// <summary>
    ///     Create / update / delete assignments at the org scope
    ///     (<c>PUT</c> + <c>DELETE /api/v1/quotas/assignments</c>).
    ///     Team-scoped (<c>quotas.assign.team</c>) and folder-scoped
    ///     (<c>quotas.assign.folder</c>) variants land in Phase 2.
    /// </summary>
    public const string AssignOrg = "quotas.assign.org";

    /// <summary>
    ///     Wildcard super-admin permission. The built-in <c>admin</c>
    ///     role carries this sentinel and the permission authorization
    ///     handler short-circuits a token bearing it.
    /// </summary>
    public const string AdminWildcard = "*";
}
