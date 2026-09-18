// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadLifecycleEvent — one row per Workload lifecycle transition.
// The aggregate appends a row on every successful Mark* call so the
// operator sees the full transition history of a workload (when it
// moved from Pending → Provisioning → Stopped → Running, when each
// start / stop / delete landed, what the failure reason was).
//
// Stored in the forge schema alongside forge.workloads so the audit
// trail stays with the workload (FK cascade). Indexed by workload id
// + occurred_at for the per-workload timeline query.
// ============================================================================

using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     Append-only audit row for a single workload lifecycle
///     transition. Inserted by <c>Workload.Mark*</c> methods; never
///     updated or deleted (the audit trail is immutable by
///     convention — even when the workload row itself is hard-deleted
///     in a future GDPR-cleanup job, the events remain as a
///     historical record, with <see cref="WorkloadId" /> pointing at
///     a deleted workload).
/// </summary>
public sealed class WorkloadLifecycleEvent
{
    /// <summary>Event row id (UUID v7 for natural time ordering).</summary>
    public Guid Id { get; init; }

    /// <summary>Parent workload.</summary>
    public WorkloadId WorkloadId { get; init; }

    /// <summary>State the workload was in before the transition.</summary>
    public WorkloadLifecycleState FromState { get; init; }

    /// <summary>State the workload moved into.</summary>
    public WorkloadLifecycleState ToState { get; init; }

    /// <summary>
    ///     Provider-assigned VM id at the moment of the transition.
    ///     Null for the initial <c>Pending → Provisioning</c> event
    ///     (the provider hasn't returned the handle yet — the
    ///     <c>Provisioning</c> event itself captures the handle
    ///     that the next <c>MarkProvisioning</c> writes).
    /// </summary>
    public string? ProviderVmId { get; init; }

    /// <summary>
    ///     Optional human-readable reason. Populated for
    ///     transitions to <see cref="WorkloadLifecycleState.Failed" />
    ///     (the provider's error message) — null for routine
    ///     transitions.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>Wall-clock the transition happened (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    ///     Construct a new event row. The id and <c>OccurredAt</c>
    ///     are caller-supplied so the Workload aggregate can mint
    ///     them deterministically (id from <c>Guid.CreateVersion7</c>
    ///     for the same workload) — the aggregate owns the clock
    ///     seam in v1, not the entity.
    /// </summary>
    /// <param name="id">UUID v7 (use <c>Guid.CreateVersion7()</c> or equivalent).</param>
    /// <param name="workloadId">Parent workload.</param>
    /// <param name="fromState">Pre-transition state.</param>
    /// <param name="toState">Post-transition state.</param>
    /// <param name="providerVmId">Provider handle, if known at this transition.</param>
    /// <param name="reason">Failure reason, if any.</param>
    /// <param name="occurredAt">Wall-clock the transition happened.</param>
    public WorkloadLifecycleEvent(
        Guid id,
        WorkloadId workloadId,
        WorkloadLifecycleState fromState,
        WorkloadLifecycleState toState,
        string? providerVmId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        Id = id;
        WorkloadId = workloadId;
        FromState = fromState;
        ToState = toState;
        ProviderVmId = providerVmId;
        Reason = reason;
        OccurredAt = occurredAt;
    }
}
