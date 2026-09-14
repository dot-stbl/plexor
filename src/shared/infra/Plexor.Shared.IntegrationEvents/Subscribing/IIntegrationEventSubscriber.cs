using Plexor.Shared.IntegrationEvents.Events;

namespace Plexor.Shared.IntegrationEvents.Subscribing;

/// <summary>
///     Subscribes a handler to an integration-event type. The implementation
///     resolves the transport topic from the event's
///     <see cref="IIntegrationEvent.EventType" />, creates a consumer for the
///     calling host and forwards every delivered message to the registered
///     handler.
/// </summary>
public interface IIntegrationEventSubscriber
{
    /// <summary>
    ///     Subscribes <paramref name="handler" /> to the topic associated
    ///     with <typeparamref name="TEvent" />. The handler runs on the
    ///     bus's consumer loop; it must be idempotent and exception-safe
    ///     (the concrete transport documents whether an uncaught throw is
    ///     logged-and-skipped or nacked).
    /// </summary>
    /// <typeparam name="TEvent">The concrete integration-event type to receive.</typeparam>
    /// <param name="handler">The handler invoked for each delivered event.</param>
    /// <param name="broadcastToAllInstances">
    ///     <c>false</c> (default) — a durable, shared consumer group per event
    ///     type: every replica of the host cooperates on one group, so the
    ///     partitions are split across them (a work-queue; each event is
    ///     handled once across the fleet). Correct for side-effecting
    ///     consumers like notification delivery. <c>true</c> — a per-process
    ///     group (the shared name plus a per-instance token), so
    ///     <b>every</b> instance receives <b>every</b> event. Correct for
    ///     fan-out that must reach all instances — e.g. pushing to WebSocket
    ///     clients each replica holds. A restarted instance gets a fresh
    ///     group and re-reads from the offset reset (on a compacted topic
    ///     that is the current per-key snapshot); the transport expires the
    ///     abandoned group after its offsets-retention window.
    /// </param>
    /// <param name="atLeastOnce">
    ///     <c>false</c> (default) — fire-and-forget: the transport dispatches
    ///     the handler and commits the offset immediately, so a handler
    ///     failure or a crash mid-handle drops the message (at-most-once).
    ///     Correct for transient fan-out where only the latest value matters
    ///     (realtime push). <c>true</c> — the transport awaits the handler
    ///     (with its retry policy) to a terminal outcome BEFORE committing
    ///     the offset, so a crash before completion redelivers the message
    ///     (at-least-once). Correct for durable side-effecting consumers
    ///     like notification delivery. Awaiting also serialises the loop,
    ///     which bounds in-flight work.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the subscription.</param>
    public ValueTask SubscribeAsync<TEvent>(
        Func<TEvent, CancellationToken, ValueTask> handler,
        bool broadcastToAllInstances = false,
        bool atLeastOnce = false,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
