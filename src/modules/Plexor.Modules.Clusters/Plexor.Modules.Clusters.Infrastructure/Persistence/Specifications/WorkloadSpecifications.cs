// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadSpecifications — reusable specs for the Workload reads.
// Identity projection (entity rows) — caller can use
// Repository<T>.ListAsync<TResult> to push a projection, or
// read full entities via Repository<T>.ListAsync(ISpecification<T>).
// ==========================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;

/// <summary>
///     Filter workloads by cluster id, ordered by creation time desc.
///     Used by the list endpoint. The FilterableFieldSet
/// (<c>WorkloadFieldSet.Instance</c>, registered at host startup)
/// applies the URL envelope's filter DSL + sort criteria + paging
/// on top of the cluster-id predicate this spec provides.
/// </summary>
public sealed class WorkloadsByClusterSpec : Specification<Workload, Workload>
{
    /// <summary>Construct from the filter parameters.</summary>
    /// <param name="clusterId">Cluster the workloads belong to.</param>
    public WorkloadsByClusterSpec(ClusterId clusterId) : base(projection: null)
    {
        WithWhere(w => w.ClusterId == clusterId);
        WithOrderByDescending(w => w.CreatedAt);
        AsNoTracking();
    }
}

/// <summary>
///     Lookup a single workload by (ClusterId, WorkloadId). Used by
///     the get-one endpoint and the delete endpoint's pre-flight
///     existence check.
/// </summary>
public sealed class WorkloadByIdSpec : Specification<Workload, Workload>
{
    /// <summary>Construct from the filter parameters.</summary>
    /// <param name="clusterId">Parent cluster.</param>
    /// <param name="workloadId">Target workload.</param>
    public WorkloadByIdSpec(ClusterId clusterId, WorkloadId workloadId) : base(projection: null)
    {
        WithWhere(w => w.ClusterId == clusterId && w.Id == workloadId);
        AsNoTracking();
    }
}
