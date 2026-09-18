// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InProcessIntegrationEventPublisherShould — behavioural tests for the
// in-process adapter. The publisher and subscriber share one instance
// (DI wires both ports to the same singleton); these tests cover the four
// guarantees that the contract promises in v0.1:
//   1. subscribe + publish round-trips to the handler
//   2. multiple subscribers all see the event
//   3. PublishBatchAsync delivers every event in order
//   4. a throwing handler is isolated — the next subscriber still runs
// ============================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Plexor.Shared.IntegrationEvents.InProcess;
using Shouldly;
using Xunit;

namespace Plexor.Shared.IntegrationEvents.Unit;

/// <summary>
///     Tests for the in-process integration-event bus. The class uses a
///     synthetic <see cref="TestEvent" /> for delivery and a fresh publisher
///     per test (the subscribe state is per-instance).
/// </summary>
public sealed class InProcessIntegrationEventPublisherShould
{
    /// <summary>
    ///     A single subscriber must see a single event after publish.
    /// </summary>
    [Fact(DisplayName = "Given a subscribed handler, when an event is published, then the handler is invoked")]
    public async Task Subscribe_then_publish_invokes_handlerAsync()
    {
        var publisher = NewPublisher();
        var observed = new List<TestEvent>();

        await publisher.SubscribeAsync<TestEvent>(
            (@event, _) =>
            {
                observed.Add(@event);
                return ValueTask.CompletedTask;
            });

        var sent = NewEvent("identity.user.created", "user-1");
        await publisher.PublishAsync(sent);

        observed.ShouldBe([sent]);
    }

    /// <summary>
    ///     Multiple subscribers for the same event type must all be invoked
    ///     in registration order.
    /// </summary>
    [Fact(DisplayName = "Given multiple subscribed handlers, when an event is published, then every handler is invoked")]
    public async Task Multiple_subscribers_all_invokedAsync()
    {
        var publisher = NewPublisher();
        var invocations = new List<int>();

        await publisher.SubscribeAsync<TestEvent>((_, _) => { invocations.Add(1); return ValueTask.CompletedTask; });
        await publisher.SubscribeAsync<TestEvent>((_, _) => { invocations.Add(2); return ValueTask.CompletedTask; });
        await publisher.SubscribeAsync<TestEvent>((_, _) => { invocations.Add(3); return ValueTask.CompletedTask; });

        await publisher.PublishAsync(NewEvent("identity.user.created", "user-1"));

        invocations.ShouldBe([1, 2, 3]);
    }

    /// <summary>
    ///     Every event in a batch must be delivered to every subscriber,
    ///     preserving the batch's order.
    /// </summary>
    [Fact(DisplayName = "Given a batch of events, when PublishBatchAsync is called, then every event is delivered in order")]
    public async Task Publish_batch_delivers_all_events_in_orderAsync()
    {
        var publisher = NewPublisher();
        var observed = new List<TestEvent>();

        await publisher.SubscribeAsync<TestEvent>(
            (@event, _) =>
            {
                observed.Add(@event);
                return ValueTask.CompletedTask;
            });

        var batch = new[]
        {
            NewEvent("identity.user.created", "user-1"),
            NewEvent("identity.user.updated", "user-1"),
            NewEvent("identity.user.deleted", "user-1"),
        };

        await publisher.PublishBatchAsync(batch);

        observed.ShouldBe(batch);
    }

    /// <summary>
    ///     A handler that throws must not stop the remaining handlers from
    ///     running, and the publisher itself must not propagate the
    ///     exception to its caller.
    /// </summary>
    [Fact(DisplayName = "Given one throwing handler and one healthy handler, when an event is published, then the healthy handler still runs and the publisher does not throw")]
    public async Task Handler_exception_does_not_break_other_subscribersAsync()
    {
        var publisher = NewPublisher();
        var healthyObserved = 0;

        await publisher.SubscribeAsync<TestEvent>(
            (_, _) => throw new InvalidOperationException("boom"));

        await publisher.SubscribeAsync<TestEvent>(
            (_, _) =>
            {
                healthyObserved++;
                return ValueTask.CompletedTask;
            });

        await publisher.PublishAsync(NewEvent("identity.user.created", "user-1"));

        healthyObserved.ShouldBe(1);
    }

    private static InProcessIntegrationEventPublisher NewPublisher()
    {
        return new InProcessIntegrationEventPublisher(
            NullLogger<InProcessIntegrationEventPublisher>.Instance);
    }

    private static TestEvent NewEvent(string eventType, string partitionKey)
    {
        return new TestEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            EventType: eventType,
            PartitionKey: partitionKey);
    }
}
