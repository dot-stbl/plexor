using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plexor.Shared.Filtering.Registry;

namespace Plexor.Shared.Filtering.DI;

/// <summary>
///     DI extension that registers per-entity filterable markers.
///     Lives in <c>Plexor.Shared.Filtering.Core</c> — pure BCL, no ASP.NET
///     Core dependency. The OpenAPI schema transformer + the
///     <c>AddFiltering()</c> composition extension live in
///     <c>Plexor.Shared.Filtering.Web</c>.
/// </summary>
/// <remarks>
///     <para><b>Usage.</b> In a module's DI installer:</para>
///     <code>
/// services
///     .AddFilterableEntity&lt;Plexor.Modules.Realm.Domain.TenantRecord&gt;()
///     .AddFilterableEntity&lt;Plexor.Modules.Audit.Domain.AuditEntry&gt;();
///     </code>
///     <para>
///         Each <c>AddFilterableEntity&lt;T&gt;()</c> registers a
///         <see cref="FilterableEntitySeed{T}" /> hosted service that
///         <see cref="FilterableEntityRegistry.Register{T}" />s the entity at
///         startup. The registry is populated by the seeds, then read by the
///         OpenAPI schema transformer (registered separately via
///         <c>AddFiltering()</c> in the Web project).
///     </para>
/// </remarks>
public static class FilteringServiceCollectionExtensions
{
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
/// <param name="registry"></param>
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
