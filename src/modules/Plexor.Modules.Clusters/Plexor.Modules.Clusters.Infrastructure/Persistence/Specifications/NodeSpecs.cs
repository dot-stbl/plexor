// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeSpecifications — reusable specs for Node reads.
// Identity projection; Repository pushes NodeSummary projection.
// ==========================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;

/// <summary>
///     Filter nodes by cluster id, ordered by creation time desc.
///     Identity projection (entity rows) — caller can use
///     <see cref="Plexor.Shared.Persistence.Repository{T}.ListAsync{TResult}" />
///     to push a projection on top, or read full entities via
///     <see cref="Plexor.Shared.Persistence.Repository{T}.ListAsync(ISpecification{T}, CancellationToken)" />.
/// </summary>
public sealed class NodesByClusterSpec : Specification<Node, Node>
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
