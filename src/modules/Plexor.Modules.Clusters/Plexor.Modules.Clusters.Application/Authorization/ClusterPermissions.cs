// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Cluster permission scope strings. Used by [RequirePermission(...)] on
// Clusters.Api controllers. The wildcard "*" admin role (seeded by
// IdentityBootstrapper) matches every entry here automatically.
// ============================================================================

namespace Plexor.Modules.Clusters.Application.Authorization;

/// <summary>
///     Permission scopes for the Clusters module. Each string is a
///     dotted scope: <c>resource.verb</c>. Bound to roles via
///     <c>sigil.role_bindings</c>; checked by
///     <c>[RequirePermission(...)]</c> at the controller.
/// </summary>
public static class ClusterPermissions
{
    /// <summary>Create a new cluster (<c>POST /api/v1/compute/clusters</c>).</summary>
    public const string Create = "compute.clusters.create";

    /// <summary>Read clusters + nodes (<c>GET /api/v1/compute/clusters/*</c>).</summary>
    public const string Read = "compute.clusters.read";

    /// <summary>Update a cluster (rename, region, rotate join token).</summary>
    public const string Update = "compute.clusters.update";

    /// <summary>Soft-delete a cluster.</summary>
    public const string Delete = "compute.clusters.delete";

    /// <summary>Read nodes per cluster (<c>GET .../clusters/{id}/nodes</c>).</summary>
    public const string NodesRead = "compute.nodes.read";
}
