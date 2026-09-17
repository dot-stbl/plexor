// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRecordSpecifications — reusable specs for NodeRecord reads.
// Identity projection (entity rows) — caller can use
// Repository<T>.ListAsync<TResult> to push a projection on top, or
// read full entities via Repository<T>.ListAsync(ISpecification<T>, ...).
// ============================================================================

using Plexor.Modules.Outpost.Application;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Outpost.Infrastructure.Persistence.Specifications;

/// <summary>
///     Filter nodes by cluster id, ordered by creation time desc.
///     Identity projection (entity rows).
/// </summary>
public sealed class NodesByClusterSpec : Specification<NodeRecord, NodeRecord>
{
    /// <summary>Construct from the filter parameters.</summary>
    /// <param name="clusterId">Cluster the nodes belong to.</param>
    public NodesByClusterSpec(ClusterId clusterId) : base(projection: null)
    {
        WithWhere(n => n.ClusterId == clusterId);
        WithOrderByDescending(n => n.CreatedAt);
        AsNoTracking();
    }
}

/// <summary>
///     Filter nodes by id, no tracking. Identity projection.
/// </summary>
public sealed class NodeByIdSpec : Specification<NodeRecord, NodeRecord>
{
    /// <summary>Construct from the target id.</summary>
    /// <param name="nodeId">Target node id.</param>
    public NodeByIdSpec(NodeId nodeId) : base(projection: null)
    {
        WithWhere(n => n.Id == nodeId);
        AsNoTracking();
    }
}