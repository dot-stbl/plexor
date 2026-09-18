// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload — the control-plane view of a workload the operator
// asked us to deploy. Records the runtime (vm / lxc / k8s.pod /
// container), the spec the operator passed, which cluster + node
// it landed on, and the current reported state.
//
// Lifecycle is split into two independent layers:
//
//   1. State (WorkloadState — Plexor.Shared.Workloads) — what the
//      NodeAgent reports back via heartbeat / on-demand poll. The
//      agent owns the local runtime (libvirt UUID, container id,
//      k3s pod name) and reports its view through LocalId + State.
//      This is the runtime mirror, written by the agent.
//
//   2. LifecycleState (WorkloadLifecycleState — this file) — the
//      host-side state machine tracking what the host has asked the
//      compute provider to do (Provisioned / Started / Stopped /
//      Deleted) and what the provider has acknowledged back. This is
//      the host's view of progress through the lifecycle; mutated
//      by the host's command handlers via Workload.Mark*.
//
// The two layers reconcile in the background: a successful
// MarkRunning sets LifecycleState = Running optimistically; the
// NodeAgent's heartbeat eventually reports State = Running too, and
// the reconciliation logic confirms the two views match. Drift
// between the two is a future reconciliation bug, not a hard error.
//
// Lives in Plexor.Modules.Clusters because the workload table
// lives in the forge schema (clusters own the node fleet that
// workloads run on). The per-runtime provider project owns its
// own LocalWorkload; this row is the durable view.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Common;
using Plexor.Shared.Workloads;

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     The control-plane's view of a workload the operator asked
///     us to deploy. Lifecycle is split into the host-side state
///     machine (<see cref="LifecycleState" /> + <see cref="MarkProvisioning" />
///     et al.) and the agent-reported runtime mirror (<see cref="State" /> +
///     <see cref="LocalId" />).
/// </summary>
public sealed class Workload : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>
    ///     Workload id. Wire format <c>wl_&lt;UUIDv7&gt;</c> —
    ///     the operator-facing handle. The NodeAgent's
    ///     <see cref="LocalWorkload" /> keeps a separate local id
    ///     (libvirt UUID, k3s pod name) that we keep in
    ///     <see cref="LocalId" /> for cross-reference.
    /// </summary>
    public WorkloadId Id { get; init; }

    /// <summary>FK to the parent cluster.</summary>
    public ClusterId ClusterId { get; init; }

    /// <summary>
    ///     FK to the assigned node. Null while the workload is
    ///     <see cref="WorkloadState.Provisioning" /> and during
    ///     transient rebalancing.
    /// </summary>
    public NodeId? AssignedNodeId { get; set; }

    /// <summary>
    ///     Stable per-runtime handle returned by the NodeAgent
    ///     (libvirt UUID, container id, k8s pod name). Lets the
    ///     host correlate a row here with a row on the node
    ///     without owning the runtime's id space. Null until
    ///     the agent reports back.
    /// </summary>
    public string? LocalId { get; set; }

    /// <summary>Operator-facing name. Unique per cluster.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Runtime identifier — "vm", "lxc", "k8s.pod", "container".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Operator-supplied configuration (image, env, ports, volumes). JSON.</summary>
    public string SpecJson { get; init; } = "{}";

    /// <summary>
    ///     Current runtime state as reported by the NodeAgent via
    ///     the heartbeat / on-demand poll. Mirrors what the local
    ///     runtime is actually doing — independent of the host's
    ///     <see cref="LifecycleState" /> (which tracks what the host
    ///     asked the provider to do).
    /// </summary>
    public WorkloadState State { get; set; }

    /// <summary>
    ///     Host-side lifecycle state. Driven by the Mark* methods
    ///     in response to <see cref="Plexor.Shared.Kernel.Compute.IComputeProvider" />
    ///     confirmations. Always present (defaults to
    ///     <see cref="WorkloadLifecycleState.Pending" /> when the
    ///     row is created).
    /// </summary>
    public WorkloadLifecycleState LifecycleState { get; set; } = WorkloadLifecycleState.Pending;

    /// <summary>
    ///     Provider-assigned VM id (libvirt domain UUID, k3s pod
    ///     UID, …). Null until <see cref="MarkProvisioning" />
    ///     lands — the provider returns the handle as part of the
    ///     CreateVmAsync acknowledgement.
    /// </summary>
    public string? ProviderVmId { get; set; }

    /// <summary>
    ///     Backing field for the <see cref="Events" /> collection.
    ///     EF Core discovers this by name and appends directly to
    ///     it during load (avoiding the cost of replacing the whole
    ///     list when the query returns multiple rows). The public
    ///     <see cref="Events" /> property exposes the same data as
    ///     a read-only list.
    /// </summary>
    private readonly List<WorkloadLifecycleEvent> _events = [];

    /// <summary>
    ///     Append-only audit trail of every successful lifecycle
    ///     transition. Loaded by the per-workload timeline query;
    ///     the per-row cardinality is small (typically 3–7 rows
    ///     per workload over its lifetime) so eager-loading is
    ///     fine for the single-workload detail endpoint.
    ///     Exposed as <see cref="IReadOnlyList{T}" /> to keep the
    ///     public surface append-only by convention — callers read
    ///     or test via the indexer but never insert / clear
    ///     outside the aggregate. EF Core discovers the backing
    ///     field by name and appends directly to it during load.
    /// </summary>
    public IReadOnlyList<WorkloadLifecycleEvent> Events => _events;

    /// <summary>Last state message from the NodeAgent (last error, etc.).</summary>
    public string? LastMessage { get; set; }

    /// <summary>When the agent last reported on this workload.</summary>
    public DateTimeOffset? LastReportedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; init; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; init; }

    // ---- Lifecycle state machine ---------------------------------------
    //
    // The Mark* methods are the canonical way to transition
    // LifecycleState. Each method validates the transition against
    // WorkloadLifecycleTransitions.IsAllowed and appends a
    // WorkloadLifecycleEvent row on success. Callers MUST NOT set
    // LifecycleState / ProviderVmId directly when driving the
    // lifecycle — go through Mark* so the audit trail stays in sync
    // with the state.

    /// <summary>
    ///     <c>Pending → Provisioning</c>. Set the
    ///     <see cref="ProviderVmId" /> the provider returned from
    ///     <see cref="Plexor.Shared.Kernel.Compute.IComputeProvider.CreateVmAsync" />.
    /// </summary>
    /// <param name="providerVmId">Provider-assigned VM id.</param>
    /// <param name="occurredAt">Wall-clock the transition happened (handler-supplied clock).</param>
    /// <exception cref="Errors.InvalidWorkloadLifecycleTransitionException">
    ///     Thrown when the current <see cref="LifecycleState" /> does
    ///     not allow a transition to <see cref="WorkloadLifecycleState.Provisioning" />.
    /// </exception>
    public void MarkProvisioning(string providerVmId, DateTimeOffset occurredAt)
    {
        TransitionTo(
            WorkloadLifecycleState.Provisioning,
            providerVmId,
            reason: null,
            occurredAt);
    }

    /// <summary>
    ///     <c>Provisioning → Stopped</c> (or <c>Running → Stopped</c>).
    ///     Provider confirmed the VM exists / powered off.
    /// </summary>
    /// <param name="occurredAt">Wall-clock the transition happened.</param>
    /// <exception cref="Errors.InvalidWorkloadLifecycleTransitionException">
    ///     Thrown when the current state does not allow Stopped.
    /// </exception>
    public void MarkStopped(DateTimeOffset occurredAt)
    {
        TransitionTo(WorkloadLifecycleState.Stopped, ProviderVmId, reason: null, occurredAt);
    }

    /// <summary>
    ///     <c>Stopped → Running</c>. Provider confirmed the VM is
    ///     powered on.
    /// </summary>
    /// <param name="occurredAt">Wall-clock the transition happened.</param>
    /// <exception cref="Errors.InvalidWorkloadLifecycleTransitionException">
    ///     Thrown when the current state is not Stopped.
    /// </exception>
    public void MarkRunning(DateTimeOffset occurredAt)
    {
        TransitionTo(WorkloadLifecycleState.Running, ProviderVmId, reason: null, occurredAt);
    }

    /// <summary>
    ///     <c>* → Failed</c> (from <see cref="WorkloadLifecycleState.Provisioning" />,
    ///     <see cref="WorkloadLifecycleState.Stopped" />, or
    ///     <see cref="WorkloadLifecycleState.Running" />). The row
    ///     stays for operator inspection; <see cref="MarkDeleting" />
    ///     is the next legal transition (Failed → Deleting).
    /// </summary>
    /// <param name="reason">Provider's failure reason (safe to surface in audit; no PII).</param>
    /// <param name="occurredAt">Wall-clock the transition happened.</param>
    /// <exception cref="Errors.InvalidWorkloadLifecycleTransitionException">
    ///     Thrown when the current state does not allow Failed.
    /// </exception>
    public void MarkFailed(string reason, DateTimeOffset occurredAt)
    {
        TransitionTo(WorkloadLifecycleState.Failed, ProviderVmId, reason, occurredAt);
    }

    /// <summary>
    ///     <c>* → Deleting</c> (from Stopped / Running / Failed).
    ///     Host dispatched <c>IComputeProvider.DeleteVmAsync</c> and
    ///     is awaiting the provider's confirmation.
    /// </summary>
    /// <param name="occurredAt">Wall-clock the transition happened.</param>
    /// <exception cref="Errors.InvalidWorkloadLifecycleTransitionException">
    ///     Thrown when the current state does not allow Deleting.
    /// </exception>
    public void MarkDeleting(DateTimeOffset occurredAt)
    {
        TransitionTo(WorkloadLifecycleState.Deleting, ProviderVmId, reason: null, occurredAt);
    }

    /// <summary>
    ///     <c>Deleting → Deleted</c>. Provider confirmed the VM is
    ///     gone. Terminal state — no further Mark* calls succeed.
    /// </summary>
    /// <param name="occurredAt">Wall-clock the transition happened.</param>
    /// <exception cref="Errors.InvalidWorkloadLifecycleTransitionException">
    ///     Thrown when the current state is not Deleting.
    /// </exception>
    public void MarkDeleted(DateTimeOffset occurredAt)
    {
        TransitionTo(WorkloadLifecycleState.Deleted, ProviderVmId, reason: null, occurredAt);
    }

    // ---- State machine internals --------------------------------------

    /// <summary>
    ///     Apply a validated lifecycle transition. Centralises the
    ///     validation + event-append so each Mark* method stays a
    ///     one-liner. NOT private — the access modifier is
    ///     <c>internal</c> so test code in the same assembly can
    ///     drive the transition directly when it needs to seed a
    ///     workload in a specific state without going through every
    ///     Mark* call. External callers (handlers) MUST use the
    ///     public Mark* methods.
    /// </summary>
    internal void TransitionTo(
        WorkloadLifecycleState toState,
        string? providerVmId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        if (!WorkloadLifecycleTransitions.IsAllowed(LifecycleState, toState))
        {
            throw new Errors.InvalidWorkloadLifecycleTransitionException(LifecycleState, toState);
        }

        var fromState = LifecycleState;
        LifecycleState = toState;
        ProviderVmId = providerVmId;

        // Clear LastMessage on successful transitions; failure
        // transitions carry the provider's reason through it.
        LastMessage = reason;

        _events.Add(new WorkloadLifecycleEvent(
            Guid.NewGuid(),
            Id,
            fromState,
            toState,
            providerVmId,
            reason,
            occurredAt));
    }
}
