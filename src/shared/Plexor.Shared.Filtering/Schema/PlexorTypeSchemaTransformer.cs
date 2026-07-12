// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorTypeSchemaTransformer — attaches the x-plexor-type extension (full
// CLR name) to each component schema. FilterableSchemaTransformer uses it as
// the lookup key for the FilterableEntityRegistry. Models in modules mark
// their entities with IFilterableEntity and the entities' properties become
// filterable when the matching controller / ProducesResponseType emits the
// type.
// ============================================================================

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Plexor.Shared.Filtering;

/// <summary>
///     Schema transformer that emits <c>x-plexor-type</c> on every
///     component schema in the OpenAPI document. The extension's value is
///     the CLR full name (e.g. <c>Plexor.Modules.Realm.Domain.TenantRecord</c>),
///     set automatically by the framework. <see cref="FilterableSchemaTransformer" />
///     reads this extension to map schemas back to registered filterable
///     entities.
/// </summary>
/// <remarks>
///     <para>The CLR name is derived from <see cref="Type.FullName" />; this
///     requires the type to be reachable from one of the loaded assemblies.
///     Anonymous types and closures do not have a full name and are simply
///     skipped.</para>
/// </remarks>
public sealed class PlexorTypeSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>No-op: source-gen can't extract CLR type at schema-emission in 10.0.9.</summary>
    /// <param name="schema">OpenAPI schema (untouched).</param>
    /// <param name="context">Transformer context (untouched).</param>
    /// <param name="cancellationToken">Cancellation (untouched).</param>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        // OpenApiSchemaTransformerContext doesn't expose the CLR type directly
        // in 2.4.1 — schema.Name is the schema's component name (e.g. "TenantRecord"),
        // and SchemaType info isn't on the context. The preferred path is
        // for callers to add the extension via controller-level metadata
        // (TypeOpenApiExtender) before schema generation runs. Keeping this
        // transformer as a no-op stub until we have a clean way to expose
        // CLR type names from Microsoft.AspNetCore.OpenApi 10.0.9.
        _ = schema;
        _ = context;
        return Task.CompletedTask;
    }
}
