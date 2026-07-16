// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadMapper — Mapperly source-generated implementation of
// IWorkloadMapper. Entity → DTO translations for read-handlers.
// The .g.cs sibling is emitted at build time by the Riok.Mapperly
// source generator; the body is just property copies for the
// single ToSummary mapping (1:1 field match).
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Plexor.Modules.Clusters.Infrastructure.Mappers;

/// <summary>
///     Mapperly-generated mapper. Implements
///     <see cref="IWorkloadMapper" />. Registered in DI as a
///     singleton via
///     <c>AddSingleton&lt;IWorkloadMapper, WorkloadMapper&gt;()</c>.
///     Source generator emits a stateless body, so a single
///     instance per host is allocation-free.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class WorkloadMapper : IWorkloadMapper
{
    /// <summary>
    ///     Map <see cref="Workload" /> to <see cref="WorkloadSummary" />.
    ///     All positional fields are mapped 1:1 by name. The
    ///     nullable <c>AssignedNodeId</c> + <c>LocalId</c> + <c>LastReportedAt</c>
    ///     fields round-trip the null value through the same DTO
    ///     fields — IFilterableEntity doesn't come into play here
    ///     (the list endpoint doesn't page through the workload
    ///     controller, it uses PageResult from the paged endpoint
    ///     wired below).
    /// </summary>
    public partial WorkloadSummary ToSummary(Workload source);
}