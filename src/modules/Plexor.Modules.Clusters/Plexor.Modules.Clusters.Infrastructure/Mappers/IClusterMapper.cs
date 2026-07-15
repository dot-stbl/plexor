// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IClusterMapper — abstraction over the module's entity → DTO mapper.
// Callers (controllers, query handlers) depend on this interface, not
// on the concrete Mapperly-generated <c>ClusterMappers</c> class.
// DI registration wires interface → concrete implementation, so
// integration tests can swap in NSubstitute mocks without pulling in
// the source-generated mapper body.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;

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
    public ClusterSummary ToSummary(Cluster source);

    /// <summary>
    ///     Map a single <see cref="Cluster" /> row + already-loaded
    ///     child nodes to a <see cref="ClusterDetail" />
    ///     (single-cluster shape). <c>nodes</c> comes from a separate
    ///     <c>NodesByClusterSpec</c> repository call.
    /// </summary>
    public ClusterDetail ToDetail(Cluster source, IReadOnlyList<NodeSummary> nodes);

    /// <summary>
    ///     Map a single <see cref="Node" /> row to a
    ///     <see cref="NodeSummary" />. Property names match 1:1.
    /// </summary>
    public NodeSummary ToNodeSummary(Node source);
}
