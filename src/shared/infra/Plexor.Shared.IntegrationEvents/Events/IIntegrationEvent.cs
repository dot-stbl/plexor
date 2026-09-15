namespace Plexor.Shared.IntegrationEvents.Events;

/// <summary>
///     Marker for any event that flows through the integration-event bus
///     (in-process for v0.1, NATS / Kafka for Phase 3 multi-host). The bus
///     is domain-agnostic: it knows only this envelope shape and routes by
///     <see cref="EventType" />. Concrete payloads implement this on their
///     own types; a per-domain specialization (e.g. a future
///     <c>INotificationEvent</c> mirroring anlytra's pattern) may add its
///     own required fields on top.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    ///     Unique event identifier (UUIDv7). Carried end-to-end so consumers
    ///     can deduplicate and correlate cross-transport handling.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    ///     When the event occurred (UTC). Stamped at the producer; the bus
    ///     does not touch it.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    ///     Discriminator the transport uses to pick the topic / subscription
    ///     group. Convention: lowercase dot.case, first segment = domain
    ///     (e.g. <c>identity.user.created</c>, <c>cluster.node.heartbeat</c>).
    /// </summary>
    public string EventType { get; }

    /// <summary>
    ///     The message key the transport partitions by — events sharing a
    ///     key land on the same partition in order. Pick the natural
    ///     aggregate identity: a user id for an identity event, a cluster id
    ///     for a cluster event. Must be non-empty and stable for the aggregate.
    /// </summary>
    public string PartitionKey { get; }
}
