// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthProviderPermissions — stable permission strings for the
// auth-providers capability. Lives in Plexor.Shared.Kernel next to
// QuotaPermissions so [RequirePermission(...)] attributes and
// role-seed code can share the same string constants without
// crossing the kernel → module boundary.
// ============================================================================

namespace Plexor.Shared.Kernel.AuthProviders;

/// <summary>
///     Stable permission strings for the auth-providers capability.
///     The wildcard <see cref="AdminWildcard" /> covers every
///     permission (including auth-provider permissions) for the
///     built-in admin role; per-operation strings like
///     <see cref="Read" /> and <see cref="Update" /> are scoped to
///     a single action.
/// </summary>
/// <remarks>
///     <para><b>Used by.</b>
///     <list type="bullet">
///       <item>
///         <c>[RequirePermission(AuthProviderPermissions.Read)]</c>
///         on <c>GET /api/v1/iam/orgs/{orgId}/auth-provider</c>
///         (4.6.1).
///       </item>
///       <item>
///         <c>[RequirePermission(AuthProviderPermissions.Update)]</c>
///         on <c>PUT /api/v1/iam/orgs/{orgId}/auth-provider</c> and
///         <c>POST /api/v1/iam/orgs/{orgId}/auth-provider/test</c>
///         (4.6.1).
///       </item>
///     </list></para>
///     <para><b>Default grants.</b> The built-in <c>admin</c> role
///     carries the <c>*</c> wildcard already seeded by
///     <c>Plexor.Migrator/IdentityBootstrapper</c>; v1 does not add
///     auth-provider permission strings to the built-in
///     <c>viewer</c> role (auth-provider config is admin-only).
///     The two strings are present so a future role catalog can
///     grant them explicitly to non-admin operator roles without
///     the wildcard.</para>
/// </remarks>
public static class AuthProviderPermissions
{
    /// <summary>
    ///     View the per-org authentication backend configuration
    ///     (<c>GET /api/v1/iam/orgs/{orgId}/auth-provider</c>). OIDC
    ///     fields are redacted in the response body (client secret
    ///     shows up as <c>"***"</c>); the permission is read-only.
    /// </summary>
    public const string Read = "org.auth.read";

    /// <summary>
    ///     Create / update the per-org authentication backend
    ///     configuration
    ///     (<c>PUT /api/v1/iam/orgs/{orgId}/auth-provider</c>) and
    ///     run the OIDC connection test
    ///     (<c>POST /api/v1/iam/orgs/{orgId}/auth-provider/test</c>).
    ///     Tenant-scoped — a user with this permission in Org X can
    ///     only mutate Org X's row.
    /// </summary>
    public const string Update = "org.auth.update";

    /// <summary>
    ///     Wildcard super-admin permission. The built-in <c>admin</c>
    ///     role carries this sentinel and the permission
    ///     authorization handler short-circuits a token bearing it.
    /// </summary>
    public const string AdminWildcard = "*";
}
