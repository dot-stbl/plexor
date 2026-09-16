// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterReadHandlers — read-only handlers using Repository<T> +
// Specification. Write handlers (CreateCluster, UpdateCluster,
// DeleteCluster, RotateJoinToken) stay on ClusterDbContext directly
// in ClusterCommandHandlers.cs. Node-join / node-heartbeat writes
// moved to Plexor.Modules.Outpost as part of the node-tracking
// extraction.
//
// Pattern matches architecture/persistence.md: reads via
// Repository<T> + Spec<T, TResult>, writes + multi-entity aggregates
// on DbContext. Paging + URL filtering routed through
// Plexor.Shared.Filtering (FilterQuery + DSL + ApplyFilter/ApplySort).
// Entity→DTO mapping via Mapperly source-generated ClusterMappers.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
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
///     Get one cluster by id. The nodes collection is loaded by a
///     separate Repository call from Plexor.Modules.Outpost (the
///     outpost schema owns the read surface now); this handler is
///     intentionally narrow — no node eager-load.
/// </summary>
/// <param name="clusterRepo">Cluster read surface.</param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
public sealed class GetClusterQueryHandler(
    Repository<Cluster> clusterRepo,
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

        return mapper.ToDetail(cluster, []);
    }
}

/// <summary>
///     List clusters in one org, paged + filterable + sortable via
///     the URL <see cref="FilterQuery" /> envelope
///     (<c>?filter=name~prod</c>, <c>?sort=name,asc</c>,
///     <c>?page=1&amp;pageSize=50</c>). Pipeline:
///     <list type="number">
///       <item><see cref="ClustersByOrgSpec" /> applies the org filter +
///       tracking flags.</item>
///       <item>Repository applies URL <c>filter</c> DSL via <see cref="QueryableFilterExtensions.ApplyFilter{T}" />.</item>
///       <item>Repository applies URL <c>sort</c> via <see cref="QueryableFilterExtensions.ApplySort{T}" />.</item>
///       <item>Repository counts the filtered set + slices the requested
///       page; returns <see cref="PageResult{T}" />.</item>
///       <item>Mapperly projects each row to <see cref="ClusterSummary" />.</item>
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