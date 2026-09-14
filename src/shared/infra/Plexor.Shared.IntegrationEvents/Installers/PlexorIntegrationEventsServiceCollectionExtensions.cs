using Microsoft.Extensions.DependencyInjection;
using Plexor.Shared.IntegrationEvents.InProcess;
using Plexor.Shared.IntegrationEvents.Publishing;
using Plexor.Shared.IntegrationEvents.Subscribing;

namespace Plexor.Shared.IntegrationEvents.Installers;

/// <summary>
///     DI registration helpers for the integration-event bus. The default
///     wiring binds the in-process adapter (single-node mode for v0.1); a
///     future NATS / Kafka adapter (Phase 3) replaces this registration with
///     the transport implementation and the rest of the codebase keeps using
///     the same two ports.
/// </summary>
public static class PlexorIntegrationEventsServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the in-process integration-event bus. Both
    ///     <see cref="IIntegrationEventPublisher" /> and
    ///     <see cref="IIntegrationEventSubscriber" /> resolve to the same
    ///     singleton instance so an in-process subscribe + publish round-trips
    ///     without a transport.
    ///     <para>
    ///         Hosts that wire a Phase 3 multi-host transport should NOT call
    ///         this — they replace it with the transport-specific extension.
    ///     </para>
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddPlexorIntegrationEvents(this IServiceCollection services)
    {
        services.AddSingleton<InProcessIntegrationEventPublisher>();
        services.AddSingleton<IIntegrationEventPublisher>(
            static sp => sp.GetRequiredService<InProcessIntegrationEventPublisher>());
        services.AddSingleton<IIntegrationEventSubscriber>(
            static sp => sp.GetRequiredService<InProcessIntegrationEventPublisher>());
        return services;
    }
}
