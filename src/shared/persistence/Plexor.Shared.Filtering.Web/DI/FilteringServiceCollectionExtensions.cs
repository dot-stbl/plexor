using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Filtering.Schema;

namespace Plexor.Shared.Filtering.DI;

/// <summary>
///     Web-side composition extension that wires the filterable-entity
///     infrastructure (registry + OpenAPI schema transformer). Lives in
///     <c>Plexor.Shared.Filtering.Web</c> because the schema transformer
///     depends on <c>Microsoft.AspNetCore.OpenApi</c>.
/// </summary>
/// <remarks>
///     <para><b>Usage.</b> In the host composition root:</para>
///     <code>
/// services.AddFiltering();
/// services.AddFilterableEntity&lt;Plexor.Modules.Realm.Domain.TenantRecord&gt;();
/// services.AddFilterableEntity&lt;Plexor.Modules.Audit.Domain.AuditEntry&gt;();
/// </code>
///     <para>
///         <see cref="AddFiltering" /> registers the registry singleton (from
///         Core) and the schema transformer (Web). The caller still needs to
///         add the transformer to the OpenAPI options separately so the
///         wiring stays in the host's composition root:
///     </para>
///     <code>
/// builder.Services.AddOpenApi()
///     .AddSchemaTransformer&lt;FilterableSchemaTransformer&gt;()
///     .AddSchemaTransformer&lt;PlexorTypeSchemaTransformer&gt;();
///     </code>
///     <para>
///         Per-entity <c>AddFilterableEntity&lt;T&gt;()</c> lives in Core (pure
///         BCL), so modules that only need the marker don't pull in ASP.NET
///         Core. See <c>Plexor.Shared.Filtering.Core.DI.FilteringServiceCollectionExtensions</c>.
///     </para>
/// </remarks>
public static class FilteringServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the filterable-entity infrastructure: the singleton
    ///     registry (from Core) and the OpenAPI schema transformer (Web).
    ///     Caller still needs <c>AddSchemaTransformer&lt;FilterableSchemaTransformer&gt;</c>
    ///     on the OpenAPI options to actually emit the extensions.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    public static IServiceCollection AddFiltering(this IServiceCollection services)
    {

        services.TryAddSingleton<FilterableEntityRegistry>();
        services.TryAddSingleton<FilterableSchemaTransformer>();

        return services;
    }
}
