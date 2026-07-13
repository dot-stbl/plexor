namespace Plexor.Shared.Kernel.Events;

/// <summary>
///     Marker interface for domain events. A domain event is a fact about
///     something that happened in the past within the domain — once
///     raised it cannot be changed. Modules raise events from their
///     Domain layer; the Application layer subscribes (typically via the
///     outbox pattern, post-v0.1) to propagate them across bounded contexts.
/// </summary>
/// <remarks>
///     <para><b>v0.1 disposition.</b> Identity module emits events from
///     controllers (via an <c>IIdentityEventPublisher</c> abstraction);
///     no in-process handler chain, no outbox, no NATS subscription. The
///     publisher writes to the audit log directly so events are persisted
///     + queryable from day one; cross-module propagation is Phase 2 (see
///     <c>architecture/persistence.md</c> outbox design).</para>
///     <para><b>Implementation contract.</b> Events are immutable records
///     with a creation timestamp; the timestamp is set by the publisher
///     (the domain layer raises a "raw" event without a timestamp; the
///     publisher wraps it with <see cref="OccurredAt" /> = <c>clock.UtcNow</c>).
///     This keeps the domain deterministic — no clock dependency in unit
///     tests.</para>
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Unique event id (UUID v7 for natural time ordering).</summary>
    public Guid EventId { get; }

    /// <summary>Wall-clock time the event was raised (UTC).</summary>
    public DateTimeOffset OccurredAt { get; }
}
