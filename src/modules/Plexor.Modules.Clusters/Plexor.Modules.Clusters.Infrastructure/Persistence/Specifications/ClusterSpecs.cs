// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterSpecifications — reusable specs for Cluster reads.
// Composable (decorator pattern): `ClustersByOrgSpec.WithRegion(...)`
// returns a new spec wrapping the base one.
//
// Identity projection (entity rows, T,T) — the Repository's PageAsync
// applies the projection on top, keeping the spec focused on
// filter/order logic only.
// ==========================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;

/// <summary>
///     Filter clusters by org id, ordered by creation time desc.
///     Identity projection (entity rows) — Repository pushes the
///     ListCards <c>ClusterSummary</c> projection on top.
/// </summary>
public sealed class ClustersByOrgSpec : Specification<Cluster, Cluster>
{
    /// <summary>Construct from the filter parameters.</summary>
    /// <param name="orgId">Tenant scope.</param>
    public ClustersByOrgSpec(Guid orgId) : base(projection: null)
    {
        WithWhere(c => c.OrgId == orgId);
        WithOrderByDescending(c => c.CreatedAt);
        AsNoTracking();
    }

    /// <summary>Compose an additional region filter onto this spec.</summary>
    public ClustersByOrgSpec WithRegion(string region)
    {
        return (ClustersByOrgSpec)WithWhere(c => c.Region == region);
    }
}

/// <summary>
///     Identity spec — load a single cluster by id, entity rows
///     (no projection). Used by <c>GetClusterQueryHandler</c>.
/// </summary>
public sealed class ClusterByIdSpec : Specification<Cluster, Cluster>
{
    /// <summary>Construct from the filter parameters.</summary>
    /// <param name="clusterId">Target cluster id.</param>
    public ClusterByIdSpec(Guid clusterId) : base(projection: null)
    {
        WithWhere(c => c.Id == clusterId);
    }
}
