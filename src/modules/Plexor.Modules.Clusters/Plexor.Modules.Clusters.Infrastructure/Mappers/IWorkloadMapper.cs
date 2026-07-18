// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IWorkloadMapper — abstraction over the workload entity → DTO
// mapper. Callers (controllers, query handlers) depend on this
// interface, not on the concrete Mapperly-generated
// ClusterMappers — wait, that's the cluster mapper. The workload
// mapper is generated as WorkloadMappers.cs and wired into DI
// against IWorkloadMapper. The interface lets integration tests
// swap in NSubstitute mocks without dragging in the source-
// generated body.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;

namespace Plexor.Modules.Clusters.Infrastructure.Mappers;

/// <summary>
///     Entity → DTO mapping contract for workloads. Bind for the
///     source generator's emitted implementation via DI:
///     <c>services.AddSingleton&lt;IWorkloadMapper, WorkloadMappers&gt;()</c>.
/// </summary>
public interface IWorkloadMapper
{
    /// <summary>
    ///     Map a single <see cref="Workload" /> row to a
    ///     <see cref="WorkloadSummary" /> (list-card shape).
    ///     Property names match 1:1 — Mapperly handles the boilerplate.
    /// </summary>
    /// <param name="source"></param>
    public WorkloadSummary ToSummary(Workload source);
}
