// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TestEvent — synthetic IIntegrationEvent used by the in-process publisher
// tests. Lives in its own file per the project's "one public type per file"
// rule; the test class references it from the same namespace.
// ============================================================================

namespace Plexor.Shared.IntegrationEvents.Unit;

/// <summary>
///     Synthetic integration event used by the in-process publisher tests.
///     Implements the four required envelope members end-to-end so the
///     adapter has something concrete to dispatch.
/// </summary>
/// <param name="EventId">Unique event id (UUIDv7 in real events; here Guid.NewGuid).</param>
/// <param name="OccurredAt">Wall-clock time the event was raised.</param>
/// <param name="EventType">Transport topic discriminator (dot.case).</param>
/// <param name="PartitionKey">Aggregate id the transport partitions by.</param>
public sealed record TestEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string EventType,
    string PartitionKey) : Plexor.Shared.IntegrationEvents.Events.IIntegrationEvent;
