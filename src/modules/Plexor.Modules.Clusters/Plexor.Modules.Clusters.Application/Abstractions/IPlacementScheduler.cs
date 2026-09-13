// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IPlacementScheduler — application-layer port for the workload
// placement scheduler. Picks a node (or returns null) for a workload
// spec, and tracks the assignment state so the next scheduling
// round can see which workloads are already pinned.
//
// The manual implementation is the v0.1 default (operator pins the
// target node through WorkloadSpec.TargetNodeId; otherwise the
// scheduler returns null and the workload stays unassigned until
// the operator acts). v0.2+ wires a policy-driven scheduler that
// fans out across capability, RAM, and disk dimensions.
// ============================================================================

using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Application.Abstractions;

/// <summary>
///     Selects a node for a new workload, or returns null if no
///     candidate is acceptable. The scheduler is the only seam that
///     decides placement policy; the handler just persists what the
///     scheduler chose.
/// </summary>
public interface IPlacementScheduler
{
    /// <summary>
    ///     Pick a node for <paramref name="spec" />, or return null
    ///     when the scheduler can't place this workload (no
    ///     candidates, or no candidate matches the policy).
    ///     Returning null is a legitimate outcome — the handler
    ///     leaves <c>AssignedNodeId</c> null and the operator decides
    ///     what to do next (pin a node manually, scale the cluster,
    ///     etc.).
    /// </summary>
    /// <param name="spec">The workload spec the operator submitted.</param>
    /// <param name="candidates">
    ///     Candidate nodes the scheduler may pick from. The caller
    ///     (handler) pre-filters by cluster / readiness; the
    ///     scheduler chooses within that list.
    /// </param>
    /// <param name="cancellationToken">Forwarded to every IO call.</param>
    public Task<NodeId?> SelectNodeAsync(
        WorkloadSpec spec,
        IReadOnlyList<NodeCandidate> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Record that <paramref name="workload" /> landed on
    ///     <paramref name="node" />. Lets a stateful scheduler
    ///     refuse a duplicate assignment or update its internal
    ///     "active workload per node" view. v0.1 is a no-op for the
    ///     manual scheduler — the assignment is already persisted
    ///     on the workload row by the handler.
    /// </summary>
    /// <param name="workload"></param>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    public Task MarkAssignedAsync(
        WorkloadId workload,
        NodeId node,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Record that <paramref name="workload" /> no longer
    ///     occupies its previous slot (delete, migrate). Counterpart
    ///     to <see cref="MarkAssignedAsync" />; v0.1 is a no-op.
    /// </summary>
    /// <param name="workload"></param>
    /// <param name="cancellationToken"></param>
    public Task ReleaseAsync(
        WorkloadId workload,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     The workload spec the scheduler sees. Mirrors the operator's
///     intent (<c>name</c>, <c>kind</c>, <c>spec.json</c>) plus an
///     optional manual pin (<c>TargetNodeId</c>). Kept minimal — the
///     full JSON spec is opaque to the scheduler; it just needs to
///     know the shape the scheduler reasons about (pin, resource
///     hints derived from kind).
/// </summary>
/// <param name="ClusterId">Target cluster — scheduler may use it to scope candidate filtering.</param>
/// <param name="Name">Operator-facing name.</param>
/// <param name="Kind">Runtime — vm / lxc / k8s.pod / container.</param>
/// <param name="TargetNodeId">
///     Manual pin. When set the scheduler must return this node if
///     it's in <c>candidates</c>; null = "pick for me".
/// </param>
/// <param name="RequiredCapabilities">
///     Capability names the workload needs from the chosen node (e.g.
///     "nested-virt" for a vm, "k3s-server" for a control-plane
///     pod). Intersected against each candidate's capability set
///     by a policy scheduler; the manual scheduler ignores this.
/// </param>
public sealed record WorkloadSpec(
    ClusterId ClusterId,
    string Name,
    string Kind,
    NodeId? TargetNodeId,
    IReadOnlyCollection<string> RequiredCapabilities);

/// <summary>
///     A candidate node the scheduler may pick. Self-contained —
///     the scheduler doesn't reach back into the Node row, it just
///     sees this snapshot. Built by the handler from the Node +
///     Capability-Report pairs the NodeAgent reported.
/// </summary>
/// <param name="NodeId">Candidate node id.</param>
/// <param name="Hostname">Self-reported OS hostname (operator-visible).</param>
/// <param name="Capabilities">
///     Capability names the node has (sorted, no duplicates).
/// </param>
/// <param name="ActiveVmCount">
///     How many workloads are already scheduled on this node.
///     Capacity hint.
/// </param>
/// <param name="AvailableRamBytes">Free RAM the agent reported.</param>
/// <param name="AvailableDiskBytes">Free disk the agent reported.</param>
public sealed record NodeCandidate(
    NodeId NodeId,
    string Hostname,
    IReadOnlyCollection<string> Capabilities,
    int ActiveVmCount,
    long AvailableRamBytes,
    long AvailableDiskBytes);
