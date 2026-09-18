// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StoragePermissions — stable permission strings for the storage
// capability. Lives in Plexor.Shared.Kernel alongside the other
// permission constants (Branding, Quotas) so [RequirePermission(...)]
// attributes and role-seed code share the same strings without
// crossing the kernel → module boundary.
// ============================================================================

namespace Plexor.Shared.Kernel.Storage;

/// <summary>
///     Stable permission strings for the storage capability. The
///     wildcard <c>*</c> covers every permission (including storage)
///     for the built-in admin role; per-operation strings like
///     <see cref="Read" /> and <see cref="Write" /> are scoped to a
///     single action.
/// </summary>
/// <remarks>
///     <para><b>Used by.</b></para>
///     <list type="bullet">
///         <item><c>[RequirePermission(StoragePermissions.Read)]</c>
///         on the GET endpoints of <c>StorageController</c>.</item>
///         <item><c>[RequirePermission(StoragePermissions.Write)]</c>
///         on the POST + DELETE endpoints that mutate volumes +
///         buckets.</item>
///         <item>The Migrator's <c>IdentityBootstrapper</c> can seed
///         <c>storage.read</c> into the built-in viewer role so
///         read-only access is available without an explicit role
///         binding; the admin role's wildcard already covers both.</item>
///     </list>
/// </remarks>
public static class StoragePermissions
{
    /// <summary>
    ///     View volumes + buckets
    ///     (<c>GET /api/v1/storage/volumes</c>,
    ///     <c>GET /api/v1/storage/buckets</c>).
    /// </summary>
    public const string Read = "storage.read";

    /// <summary>
    ///     Create / update / delete volumes + buckets
    ///     (<c>POST /api/v1/storage/volumes</c>,
    ///     <c>DELETE /api/v1/storage/volumes/{id}</c>,
    ///     <c>POST /api/v1/storage/buckets</c>,
    ///     <c>DELETE /api/v1/storage/buckets/{id}</c>).
    ///     Tenant-scoped: a caller with this permission can only
    ///     mutate resources in their own org (the controller compares
    ///     the supplied orgId against the caller's
    ///     <c>currentUser.OrgId</c>).
    /// </summary>
    public const string Write = "storage.write";
}
