using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Plexor.Shared.IntegrationEvents.Events;
using Plexor.Shared.IntegrationEvents.Publishing;
using Plexor.Shared.IntegrationEvents.Subscribing;

namespace Plexor.Shared.IntegrationEvents.InProcess;

/// <summary>
///     Single-host integration-event bus. Both <see cref="IIntegrationEventPublisher" />
///     and <see cref="IIntegrationEventSubscriber" /> resolve to one instance,
///     so <see cref="PublishAsync{TEvent}" /> dispatches the event straight to
///     every <see cref="SubscribeAsync{TEvent}" />-registered handler inside
///     the calling scope.
///     <para>
///         This is the v0.1 default — no transport, no broker, no outbox
///         relay. Multi-host mode (NATS / Kafka) lands in Phase 3 as a
///         sibling adapter that honours <c>broadcastToAllInstances</c> and
///         <c>atLeastOnce</c>; the
///         contract surface stays stable.
///     </para>
///     <para>
///         <b>Exception isolation.</b> A throwing handler is logged and
///         skipped — the remaining handlers still run, and the next event in
///         a batch is still dispatched. The publisher itself never throws on
///         a handler fault (a logger / observer bug must not take the bus
///         down).
///     </para>
/// </summary>
/// <param name="logger">Logger for handler exceptions and diagnostics.</param>
public sealed class InProcessIntegrationEventPublisher(
    ILogger<InProcessIntegrationEventPublisher> logger)
    : IIntegrationEventPublisher,
      IIntegrationEventSubscriber
{
    private readonly ConcurrentDictionary<Type, ImmutableArray<Delegate>> handlersByType = new();

    /// <inheritdoc />
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        var snapshot = handlersByType.GetValueOrDefault(typeof(TEvent));
        if (snapshot.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var handler in snapshot)
        {
            try
            {
                var typedHandler = (Func<TEvent, CancellationToken, ValueTask>)handler;
                await typedHandler(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Integration-event handler for {EventType} threw; continuing with remaining subscribers",
                    typeof(TEvent).Name);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask PublishBatchAsync<TEvent>(
        IReadOnlyCollection<TEvent> events,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        if (events.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(PublishAllAsync(events, cancellationToken));

        async Task PublishAllAsync(IReadOnlyCollection<TEvent> items, CancellationToken ct)
        {
            foreach (var @event in items)
            {
                await PublishAsync(@event, ct);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask SubscribeAsync<TEvent>(
        Func<TEvent, CancellationToken, ValueTask> handler,
        bool broadcastToAllInstances = false,
        bool atLeastOnce = false,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        // broadcastToAllInstances + atLeastOnce are no-ops in single-host
        // mode: every subscriber is in this process, runs to completion
        // before the next event is dispatched, and the publish call awaits
        // it (atLeastOnce-style) inside the caller's scope. Multi-host
        // transport (Phase 3) honours them.
        // cancellationToken is also a no-op here — subscribe itself never
        // blocks; a cancellation request would target the host shutdown
        // path, not this registration call.

        // Lock-free subscribe: GetOrAdd ensures the key exists with an
        // empty handler set on first call; TryUpdate then atomically swaps
        // in the appended snapshot, looping on lost races.
        // GetOrAdd is preferred over TryAdd alone because it short-circuits
        // the second-lookup when another thread wins the race to insert.
        var eventType = typeof(TEvent);
        var newHandler = (Delegate)handler;
        while (true)
        {
            var current = handlersByType.GetOrAdd(
                eventType,
                static _ => []);
            var next = current.Add(newHandler);
            if (handlersByType.TryUpdate(eventType, next, current))
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
