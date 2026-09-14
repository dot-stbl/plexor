// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ManualPlacementScheduler — v0.1 IPlacementScheduler. The operator
// pins the target node via WorkloadSpec.TargetNodeId; if unset the
// scheduler returns null and the workload stays unassigned.
//
// MarkAssignedAsync + ReleaseAsync are no-ops on this implementation:
// the assignment is already persisted by the handler on the
// workload row, and the scheduler doesn't keep its own state. v0.2+
// will introduce a stateful scheduler (capacity tracker, anti-affinity
// rules) that does write here.
// ============================================================================

using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Infrastructure.Placement;

/// <summary>
///     Manual placement policy. Returns the operator-pinned target
///     node when <see cref="WorkloadSpec.TargetNodeId" /> is set and
///     the node is in the candidate list; otherwise returns null. The
///     scheduler never picks a node on its own — the operator must
///     commit to the placement first.
/// </summary>
public sealed class ManualPlacementScheduler : IPlacementScheduler
{
    /// <inheritdoc />
    public Task<NodeId?> SelectNodeAsync(
        WorkloadSpec spec,
        IReadOnlyList<NodeCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        // No pin → operator hasn't decided → leave unassigned.
        // The handler propagates the null back to the caller so
        // the operator sees the workload in Pending placement state.
        if (spec.TargetNodeId is not { } pinnedNodeId)
        {
            return Task.FromResult<NodeId?>(null);
        }

        // Pin set but candidate list is empty → can't validate the
        // pin (the operator may have typed a stale id). Refuse to
        // place rather than assign blindly; the handler surfaces
        // the InvalidWorkloadSpec error.
        if (candidates.Count == 0)
        {
            return Task.FromResult<NodeId?>(null);
        }

        // Pin set + candidates present → return the pinned node
        // if it's in the list; null otherwise. We don't second-guess
        // capability / capacity here — the manual policy trusts the
        // operator; the policy scheduler will intersect capability
        // sets when it arrives in v0.2.
        var match = candidates
            .FirstOrDefault(candidate => candidate.NodeId == pinnedNodeId);

        return Task.FromResult<NodeId?>(match?.NodeId);
    }

    /// <inheritdoc />
    public Task MarkAssignedAsync(
        WorkloadId workload,
        NodeId node,
        CancellationToken cancellationToken = default)
    {
        // No-op: the manual scheduler doesn't keep its own state.
        // The assignment is durable on the Workload row (AssignedNodeId)
        // which the handler already wrote. A future capacity tracker
        // would bump ActiveVmCount here.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(
        WorkloadId workload,
        CancellationToken cancellationToken = default)
    {
        // No-op: counter to MarkAssignedAsync; the Workload row's
        // AssignedNodeId will be nulled by the handler that called
        // us (delete / migrate). The capacity tracker would decrement
        // here.
        return Task.CompletedTask;
    }
}
