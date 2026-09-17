// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlacementCandidateLoader — projects ClusterDbContext Node rows into
// the scheduler's NodeCandidate form. Sc Dated lifetime; one instance
// per request, sharing the same DbContext as the handler.
// ============================================================================
//
// The capability collection is empty for v0.1 because the control
// plane doesn't yet persist per-node capability reports (those arrive
// via the NodeAgent's heartbeat reconciliation, a later sprint). The
// manual scheduler doesn't read capabilities, so the empty list is
// safe today; the policy scheduler in v0.2 will populate this from
// the heartbeat-collected capability reports.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Infrastructure.Placement;

/// <summary>
///     Loads Ready nodes in a cluster and projects them into the
///     scheduler's <see cref="NodeCandidate" /> shape. Extracted from
///     <c>CreateWorkloadCommandHandler</c> so the handler stays
///     orchestration-only (no private business logic — see
///     <c>code-shape.md §9</c>).
/// </summary>
/// <param name="nodeRegistry">Outpost's node registry (single source of truth for nodes post-extraction).</param>
public sealed class PlacementCandidateLoader(INodeRegistry nodeRegistry)
{
    /// <summary>
    ///     Read Ready nodes in <paramref name="clusterId" /> and
    ///     project them to <see cref="NodeCandidate" />. Draining and
    ///     Gone nodes are excluded — the scheduler never picks a
    ///     node that isn't eligible for new workloads. Returns an
    ///     empty list when no Ready nodes exist in the cluster.
    /// </summary>
    /// <param name="clusterId">Target cluster.</param>
    /// <param name="cancellationToken">Forwarded to the query.</param>
    public async Task<IReadOnlyList<NodeCandidate>> LoadAsync(
        ClusterId clusterId,
        CancellationToken cancellationToken = default)
    {
        var nodeRecords = await nodeRegistry.ListNodesAsync(clusterId, cancellationToken);
        var candidates = new List<NodeCandidate>(nodeRecords.Count);
        foreach (var node in nodeRecords)
        {
            if (node.Status != Plexor.Modules.Outpost.Application.NodeStatus.Ready)
            {
                continue;
            }

            // VmCount = capacity hint (how many workloads are
            // already on this node). Free RAM / disk aren't yet
            // tracked per-node in the control plane; pass 0 as a
            // placeholder until heartbeat reconciliation populates
            // them.
            candidates.Add(new NodeCandidate(
                NodeId: node.Id,
                Hostname: node.Hostname,
                Capabilities: node.Spec.Providers,
                ActiveVmCount: node.VmCount,
                AvailableRamBytes: 0,
                AvailableDiskBytes: 0));
        }

        return candidates;
    }
}
