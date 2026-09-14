// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingPermissions — stable permission strings for the branding
// capability. Lives in Plexor.Shared.Kernel so [RequirePermission(...)]
// attributes and role-seed code can share the same constants without
// crossing the kernel → module boundary. Mirrors QuotaPermissions.
// ============================================================================

namespace Plexor.Shared.Kernel.Branding;

/// <summary>
///     Stable permission strings for the branding capability. The
///     wildcard <c>*</c> covers every permission (including
///     branding) for the built-in admin role; per-operation strings
///     like <see cref="Read" /> and <see cref="Update" /> are scoped
///     to a single action.
/// </summary>
/// <remarks>
///     <para><b>Used by.</b></para>
///     <list type="bullet">
///         <item><c>[RequirePermission(BrandingPermissions.Read)]</c>
///         on the GET endpoints of <c>BrandingController</c>.</item>
///         <item><c>[RequirePermission(BrandingPermissions.Update)]</c>
///         on the PUT / DELETE endpoints that mutate the operator or
///         tenant branding row.</item>
///         <item>The Migrator's <c>IdentityBootstrapper</c> can seed
///         <c>branding.read</c> into the built-in viewer role so
///         read-only access is available without an explicit role
///         binding; the admin role's wildcard already covers both.</item>
///     </list>
/// </remarks>
public static class BrandingPermissions
{
    /// <summary>
    ///     View the operator global config + per-org overrides
    ///     (<c>GET /api/v1/branding/global</c> +
    ///     <c>GET /api/v1/branding/org/{orgId}</c>).
    /// </summary>
    public const string Read = "branding.read";

    /// <summary>
    ///     Create / update / delete the operator global config and
    ///     per-org overrides
    ///     (<c>PUT /api/v1/branding/global</c>,
    ///     <c>PUT</c> + <c>DELETE /api/v1/branding/org/{orgId}</c>).
    ///     Tenant-scoped: a caller with this permission can only mutate
    ///     their own org's override row (the controller compares the
    ///     supplied orgId against the caller's
    ///     <c>currentUser.OrgId</c>).
    /// </summary>
    public const string Update = "branding.update";
}