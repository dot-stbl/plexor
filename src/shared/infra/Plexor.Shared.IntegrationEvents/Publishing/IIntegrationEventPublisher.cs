using Plexor.Shared.IntegrationEvents.Events;

namespace Plexor.Shared.IntegrationEvents.Publishing;

/// <summary>
///     Publishes integration events onto the bus. The implementation resolves
///     the transport topic from each event's
///     <see cref="IIntegrationEvent.EventType" />, partitions by
///     <see cref="IIntegrationEvent.PartitionKey" />, and owns the
///     serialization, retries and tracing the chosen transport requires.
///     <para>
///         Two shapes, same transport: <see cref="PublishAsync{TEvent}" /> for
///         a single point change, and <see cref="PublishBatchAsync{TEvent}" />
///         for a whole collection at once — the batch overload maps straight
///         onto a producer whose upstream already coalesces (e.g. an ingest
///         projection that bulk-upserts a flush to the DB and hands that same
///         batch here), so one DB flush becomes one Kafka batch instead of a
///         call per row.
///     </para>
///     <para>
///         v0.1 ships only the in-process adapter
///         (<see cref="Plexor.Shared.IntegrationEvents.InProcess.InProcessIntegrationEventPublisher" />),
///         which invokes every registered subscriber synchronously inside the
///         calling scope. Multi-host transport (NATS / Kafka) lands in Phase 3
///         as a sibling adapter — the contract surface stays stable across
///         both.
///     </para>
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    ///     Publishes a single <paramref name="event" /> onto the bus. The
    ///     call returns once the transport has accepted the message;
    ///     downstream at-least-once delivery is the implementation's
    ///     responsibility.
    /// </summary>
    /// <typeparam name="TEvent">The concrete integration-event type to publish.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token to cancel the publish operation.</param>
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;

    /// <summary>
    ///     Publishes a whole <paramref name="events" /> collection onto the
    ///     bus in one batch — every event is queued to the transport and the
    ///     call returns once they are all accepted. Each event keeps its own
    ///     topic (by <see cref="IIntegrationEvent.EventType" />) and key (by
    ///     <see cref="IIntegrationEvent.PartitionKey" />), so a batch of
    ///     same-typed events lands on one topic spread across partitions by
    ///     key. An empty collection is a no-op.
    /// </summary>
    /// <typeparam name="TEvent">The concrete integration-event type to publish.</typeparam>
    /// <param name="events">The events to publish; empty is a no-op.</param>
    /// <param name="cancellationToken">Token to cancel the publish operation.</param>
    public ValueTask PublishBatchAsync<TEvent>(
        IReadOnlyCollection<TEvent> events,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
