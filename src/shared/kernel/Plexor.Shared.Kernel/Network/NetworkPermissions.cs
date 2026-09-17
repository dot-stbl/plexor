// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkPermissions — stable permission strings for the network
// capability. Lives in Plexor.Shared.Kernel alongside the other
// permission constants (Branding, Quotas, Storage) so
// [RequirePermission(...)] attributes and role-seed code share the
// same strings without crossing the kernel → module boundary.
// ============================================================================

namespace Plexor.Shared.Kernel.Network;

/// <summary>
///     Stable permission strings for the network capability. The
///     wildcard <c>*</c> covers every permission (including
///     network) for the built-in admin role; per-operation strings
///     like <see cref="Read" /> and <see cref="Write" /> are
///     scoped to a single action.
/// </summary>
/// <remarks>
///     <para><b>Used by.</b></para>
///     <list type="bullet">
///         <item><c>[RequirePermission(NetworkPermissions.Read)]</c>
///         on the GET endpoints of the network REST surface.</item>
///         <item><c>[RequirePermission(NetworkPermissions.Write)]</c>
///         on the POST + DELETE endpoints that mutate floating IPs +
///         load balancers.</item>
///     </list>
/// </remarks>
public static class NetworkPermissions
{
    /// <summary>
    ///     View floating IPs + load balancers
    ///     (<c>GET /api/v1/network/floating-ips</c>,
    ///     <c>GET /api/v1/network/load-balancers</c>).
    /// </summary>
    public const string Read = "network.read";

    /// <summary>
    ///     Create / update / delete floating IPs + load balancers
    ///     (<c>POST /api/v1/network/floating-ips</c>,
    ///     <c>DELETE /api/v1/network/floating-ips/{id}</c>,
    ///     <c>POST /api/v1/network/load-balancers</c>,
    ///     <c>DELETE /api/v1/network/load-balancers/{id}</c>).
    ///     Tenant-scoped: a caller with this permission can only
    ///     mutate resources in their own org.
    /// </summary>
    public const string Write = "network.write";
}
