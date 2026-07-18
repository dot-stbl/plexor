// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterReadHandlers — read-only handlers using Repository<T> +
// Specification. Write handlers (CreateCluster, UpdateCluster,
// DeleteCluster, RotateJoinToken, NodeJoin's writes, NodeHeartbeat)
// stay on ClusterDbContext directly in ClusterCommandHandlers.cs /
// NodeCommandHandlers.cs.
//
// Pattern matches architecture/persistence.md: reads via
// Repository<T> + Spec<T, TResult>, writes + multi-entity aggregates
// on DbContext. Paging + URL filtering routed through
// Plexor.Shared.Filtering (FilterQuery + DSL + ApplyFilter/ApplySort).
// Entity→DTO mapping via Mapperly source-generated ClusterMappers.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering.Persistence;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     Get one cluster by id with its child nodes loaded. Read
///     surface — goes through the repository. The nodes collection
///     is loaded by a separate Repository call (no eager-load
///     collection property on the entity).
/// </summary>
/// <param name="clusterRepo">Cluster read surface.</param>
/// <param name="nodeRepo">Node read surface.</param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
public sealed class GetClusterQueryHandler(
    Repository<Cluster> clusterRepo,
    Repository<Node> nodeRepo,
    IClusterMapper mapper) : ICommandHandler<GetClusterQuery, ClusterDetail>
{
    /// <inheritdoc />
    public async Task<ClusterDetail> HandleAsync(
        GetClusterQuery command,
        CancellationToken cancellationToken = default)
    {
        if (await clusterRepo.FirstOrDefaultAsync(
                new ClusterByIdSpec(command.ClusterId),
                cancellationToken) is not { } cluster)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");
        }

        var nodes = await nodeRepo.ListAsync(
            new NodesByClusterSpec(command.ClusterId),
            n => mapper.ToNodeSummary(n),
            cancellationToken);

        return mapper.ToDetail(cluster, nodes);
    }
}

/// <summary>
///     List clusters in one org, paged + filterable + sortable via
///     the URL <see cref="FilterQuery" /> envelope
///     (<c>?filter=name~prod</c>, <c>?sort=name,asc</c>,
///     <c>?page=1&amp;pageSize=50</c>). Pipeline:
///     <list type="number">
///       <item><see cref="ClustersByOrgSpec" /> applies the org filter +
///       tracking flags</item>
///       <li>Repository applies URL <c>filter</c> DSL via <see cref="QueryableFilterExtensions.ApplyFilter{T}" /></li>
///       <li>Repository applies URL <c>sort</c> via <see cref="QueryableFilterExtensions.ApplySort{T}" /></li>
///       <li>Repository counts the filtered set + slices the requested
///       page; returns <see cref="PageResult{T}" /></li>
///       <li>Mapperly projects each row to <see cref="ClusterSummary" /></li>
///     </list>
/// </summary>
/// <param name="clusterRepo">Cluster read surface.</param>
/// <param name="fields">Per-entity field registry — usually
/// <c>ClusterFieldSet.Instance</c>, registered at host startup.</param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
public sealed class ListClustersQueryHandler(
    Repository<Cluster> clusterRepo,
    FilterableFieldSet<Cluster> fields,
    IClusterMapper mapper)
    : ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>
{
    /// <inheritdoc />
    public async Task<PageResult<ClusterSummary>> HandleAsync(
        ListClustersQuery command,
        CancellationToken cancellationToken = default)
    {
        return await clusterRepo.PageAsync(
            new ClustersByOrgSpec(command.OrgId),
            c => mapper.ToSummary(c),
            command.Query,
            fields,
            cancellationToken);
    }
}

/// <summary>
///     List nodes in one cluster — read via <see cref="NodesByClusterSpec" />.
/// </summary>
/// <param name="nodeRepo">Node read surface.</param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
public sealed class ListNodesQueryHandler(
    Repository<Node> nodeRepo,
    IClusterMapper mapper)
    : ICommandHandler<ListNodesQuery, IReadOnlyList<NodeSummary>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<NodeSummary>> HandleAsync(
        ListNodesQuery command,
        CancellationToken cancellationToken = default)
    {
        return await nodeRepo.ListAsync(
            new NodesByClusterSpec(command.ClusterId),
            n => mapper.ToNodeSummary(n),
            cancellationToken);
    }
}
