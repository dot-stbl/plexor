// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadLifecycleChanged — domain event raised when a Workload's
// LifecycleState transitions. Fired by Workload.Mark* on every
// successful transition; consumed by the Audit module (and any future
// outbox + cross-module subscribers).
//
// v1 disposition. No in-process dispatcher yet — the event type
// exists so the Audit module can subscribe when the cross-module
// event bus lands. Until then the persistent WorkloadLifecycleEvent
// row in forge.workload_lifecycle_events is the audit trail.
// ============================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Events;

namespace Plexor.Modules.Clusters.Domain.Events;

/// <summary>
///     Immutable record describing a single workload lifecycle
///     transition. Raised from <c>Workload.Mark*</c> on every
///     successful state change. Carries the transition pair
///     (<see cref="FromState" />, <see cref="ToState" />), the
///     provider's VM id at the moment of the transition, and an
///     optional <see cref="Reason" /> for failure transitions.
/// </summary>
/// <param name="WorkloadId">Wire id of the workload that transitioned.</param>
/// <param name="ClusterId">Parent cluster (scope for downstream subscribers).</param>
/// <param name="FromState">State the workload was in before the transition.</param>
/// <param name="ToState">State the workload moved into.</param>
/// <param name="ProviderVmId">Provider handle at the moment of the transition (null if not yet assigned).</param>
/// <param name="Reason">Human-readable reason (populated for <c>→ Failed</c> transitions).</param>
/// <param name="EventId">Unique event id (UUID v7 for natural time ordering).</param>
/// <param name="OccurredAt">Wall-clock the transition happened (UTC).</param>
public sealed record WorkloadLifecycleChanged(
    WorkloadId WorkloadId,
    ClusterId ClusterId,
    WorkloadLifecycleState FromState,
    WorkloadLifecycleState ToState,
    string? ProviderVmId,
    string? Reason,
    Guid EventId,
    DateTimeOffset OccurredAt) : IDomainEvent;
