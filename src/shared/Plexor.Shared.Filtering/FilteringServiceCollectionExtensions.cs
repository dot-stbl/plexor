using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Microsoft.AspNetCore.OpenApi;

namespace Plexor.Shared.Filtering;

/// <summary>
///     DI extension to wire <see cref="IFilterableEntity" /> registration,
///     the <see cref="FilterableEntityRegistry" />, and the
///     <see cref="FilterableSchemaTransformer" />.
/// </summary>
/// <remarks>
///     <para><b>Usage.</b> In a module's DI installer:</para>
///     <code>
/// services
///     .AddFiltering()
///     .AddFilterableEntity&lt;Plexor.Modules.Tenants.Domain.TenantRecord&gt;()
///     .AddFilterableEntity&lt;Plexor.Modules.Audit.Domain.AuditEntry&gt;();
///     </code>
///     <para><b>Wire-up ordering.</b> Call <see cref="AddFiltering" /> first
///     (registers the registry + transformer), then one
///     <see cref="AddFilterableEntity{T}" /> per entity. Calling order
///     does not affect emission output — the transformer pulls the full
///     registry on every schema pass.</para>
///     <para><b>OpenAPI registration.</b> <see cref="AddFiltering" /> does
///     NOT itself register the transformer on the OpenAPI builder; the
///     host calls <c>AddOpenApi().AddSchemaTransformer&lt;FilterableSchemaTransformer&gt;()</c>
///     separately so the wiring stays in the host's composition root.</para>
/// </remarks>
public static class FilteringServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the filterable-entity infrastructure: the singleton
    ///     registry and the schema transformer. Caller still needs to call
    ///     <c>AddSchemaTransformer&lt;FilterableSchemaTransformer&gt;</c> on
    ///     the OpenAPI options to actually emit the extensions.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    public static IServiceCollection AddFiltering(this IServiceCollection services)
    {

        services.TryAddSingleton<FilterableEntityRegistry>();
        services.TryAddSingleton<FilterableSchemaTransformer>();

        return services;
    }

    /// <summary>
    ///     Registers <typeparamref name="T" /> as filterable. Emits its
    ///     properties as <c>x-filterable</c> on the matching OpenAPI schema
    ///     (matched via <c>x-plexor-type</c> extension).
    /// </summary>
    /// <typeparam name="T">Entity type implementing <see cref="IFilterableEntity" />.</typeparam>
    /// <param name="services">DI service collection.</param>
    public static IServiceCollection AddFilterableEntity<T>(this IServiceCollection services)
        where T : IFilterableEntity
    {

        services.AddSingleton<FilterableEntitySeed<T>>();

        return services;
    }
}

/// <summary>
///     Decorator that registers an entity with the registry on first
///     construction. Resolved automatically when an entity is added via
///     <see cref="FilteringServiceCollectionExtensions.AddFilterableEntity{T}" />;
///     nothing in user code should construct this type directly.
/// </summary>
/// <typeparam name="T">Entity to seed.</typeparam>
internal sealed class FilterableEntitySeed<T>(FilterableEntityRegistry registry) : IHostedService
    where T : IFilterableEntity
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        registry.Register<T>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
