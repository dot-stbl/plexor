// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditPermissions — stable permission strings for the audit capability.
// Lives in Plexor.Shared.Kernel next to AuditActions + IAuditEmitter so
// [RequirePermission(...)] attributes and role-seed code share the same
// string constants without crossing the kernel → module boundary. Same
// shape as QuotaPermissions / AuthProviderPermissions.
// ============================================================================

namespace Plexor.Shared.Kernel.Audit;

/// <summary>
///     Stable permission strings for the audit capability. The
///     wildcard <see cref="AdminWildcard" /> covers every
///     permission (including audit) for the built-in admin role;
///     <see cref="Read" /> is the single per-operation string
///     v1 ships.
/// </summary>
/// <remarks>
///     <para><b>Used by.</b>
///     <list type="bullet">
///       <item>
///         <c>[RequirePermission(AuditPermissions.Read)]</c> on
///         <c>GET /api/v1/audit</c> (5.2).
///       </item>
///     </list></para>
///     <para><b>Default grants.</b> The built-in <c>admin</c>
///     role carries the <c>*</c> wildcard already seeded by
///     <c>Plexor.Migrator/IdentityBootstrapper</c>; v1 does not
///     add the audit read permission to the built-in
///     <c>viewer</c> role (the audit timeline is admin-only —
///     viewing it leaks who-did-what metadata that the operator
///     wouldn't normally expose to tenant viewers).</para>
/// </remarks>
public static class AuditPermissions
{
    /// <summary>
    ///     View the tenant-scoped audit timeline
    ///     (<c>GET /api/v1/audit</c>). Read-only; the emit path
    ///     is not gated by a permission string — every authenticated
    ///     request can trigger an emit through its feature
    ///     endpoint, and the audit row records the actor.
    /// </summary>
    public const string Read = "audit.read";

    /// <summary>
    ///     Wildcard super-admin permission. The built-in
    ///     <c>admin</c> role carries this sentinel and the
    ///     permission authorization handler short-circuits a token
    ///     bearing it.
    /// </summary>
    public const string AdminWildcard = "*";
}
