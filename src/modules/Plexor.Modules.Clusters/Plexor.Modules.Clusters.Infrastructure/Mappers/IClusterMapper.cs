// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IClusterMapper — abstraction over the module's entity → DTO mapper.
// Callers (controllers, query handlers) depend on this interface, not
// on the concrete Mapperly-generated <c>ClusterMappers</c> class.
// DI registration wires interface → concrete implementation, so
// integration tests can swap in NSubstitute mocks without pulling in
// the source-generated mapper body.
//
// Node tracking moved to Plexor.Modules.Outpost; the ClusterDetail
// DTO's <c>Nodes</c> collection is now empty + populated by the
// Outpost read handler, not by this mapper.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;

namespace Plexor.Modules.Clusters.Infrastructure.Mappers;

/// <summary>
///     Entity → DTO mapping contract for the Clusters module. Bind
///     for the source generator's emitted implementation via DI:
///     <c>services.AddSingleton&lt;IClusterMapper, ClusterMappers&gt;()</c>.
/// </summary>
public interface IClusterMapper
{
    /// <summary>
    ///     Map a single <see cref="Cluster" /> row to a
    ///     <see cref="ClusterSummary" /> (list-card shape).
    /// </summary>
    /// <param name="source"></param>
    public ClusterSummary ToSummary(Cluster source);

    /// <summary>
    ///     Map a single <see cref="Cluster" /> row to a
    ///     <see cref="ClusterDetail" /> (single-cluster shape). The
    ///     Nodes collection is populated by the Outpost-side handler;
    ///     this mapper only knows about the forge-side row.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="nodes">Optional pre-loaded nodes (empty for v0.1).</param>
    public ClusterDetail ToDetail(Cluster source, IReadOnlyList<ClusterNodeSummary> nodes);
}