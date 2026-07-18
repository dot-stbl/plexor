// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload read-handlers — List / Get. Co-located in one file
// because both depend on the same Repository<Workload> +
// IWorkloadMapper, and the bodies are < 30 lines each. Pattern
// mirrors ClusterReadHandlers.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     List workloads in a cluster, paged + filtered via the
///     standard <see cref="Plexor.Shared.Filtering.Query.FilterQuery" />
///     URL envelope. The cluster-id predicate is in the spec; the
///     FilterableFieldSet applies the rest of the envelope (filter
///     DSL + sort + paging).
/// </summary>
/// <param name="workloadRepo">Repository for paged read.</param>
/// <param name="fields">Per-entity field set, reflected at startup.</param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
public sealed class ListWorkloadsQueryHandler(
    Repository<Workload> workloadRepo,
    FilterableFieldSet<Workload> fields,
    IWorkloadMapper mapper)
    : ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>
{
    /// <inheritdoc />
    public async Task<PageResult<WorkloadSummary>> HandleAsync(
        ListWorkloadsQuery command,
        CancellationToken cancellationToken = default)
    {
        return await workloadRepo.PageAsync(
            new WorkloadsByClusterSpec(command.ClusterId),
            w => mapper.ToSummary(w),
            command.Query,
            fields,
            cancellationToken);
    }
}

/// <summary>Fetch one workload by id.</summary>
/// <param name="workloadRepo">Repository for the read.</param>
/// <param name="mapper">Entity → DTO mapper.</param>
public sealed class GetWorkloadQueryHandler(
    Repository<Workload> workloadRepo,
    IWorkloadMapper mapper)
    : ICommandHandler<GetWorkloadQuery, WorkloadSummary>
{
    /// <inheritdoc />
    public async Task<WorkloadSummary> HandleAsync(
        GetWorkloadQuery command,
        CancellationToken cancellationToken = default)
    {
        var workload = await workloadRepo.FirstOrDefaultAsync(
            new WorkloadByIdSpec(command.ClusterId, command.WorkloadId),
            cancellationToken)
            ?? throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' not found in cluster '{command.ClusterId}'.");

        return mapper.ToSummary(workload);
    }
}
