// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FilterableSchemaTransformer — Plexor.OpenApi schema transformer that attaches
// the x-filterable + x-sortable extensions to schemas whose CLR type is
// registered via AddFilterableEntity<T>. The frontend kubb plugin reads these
// extensions and generates typed fluent filter builders per entity.
//
// Targets Microsoft.OpenApi 2.4.1 (the version Microsoft.AspNetCore.OpenApi
// 10.0.9 transitively pins). The 3.x API surface changed (Example became
// read-only) and is not yet supported by Microsoft.AspNetCore.OpenApi's
// source generator; tracked in .planning/BACKEND-ISSUES.md.
// ============================================================================

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Plexor.Shared.Filtering;

/// <summary>
///     Schema transformer that walks every component-schema in the OpenAPI
///     document and attaches <c>x-filterable</c> + <c>x-sortable</c> to the
///     ones whose name matches a registered <see cref="IFilterableEntity" />.
/// </summary>
/// <remarks>
///     <para><b>Pairing algorithm.</b> The transformer reads the schema's
///     <c>x-plexor-type</c> extension (set via
///     <see cref="PlexorTypeSchemaTransformer" />, or manually on the schema
///     generator) to obtain the CLR type's full name, then asks
///     <see cref="FilterableEntityRegistry" /> for the registered
///     <see cref="UntypedFilterableField" />s. Fields with
///     <see cref="FilterOperator.None" /> are excluded from
///     <c>x-filterable.fields</c>; all field names appear in
///     <c>x-sortable</c> (the parser falls back to default ordering when a
///     sort criterion names a field the field set does not know).</para>
///     <para><b>Output format.</b> Per-filter field entry:</para>
///     <code>
/// {
///   "name": "Name",
///   "type": "string",
///   "operators": ["eq","notEq","contains","startsWith","endsWith",
///                 "in","iContains","iStartsWith","iEndsWith","isNull","isNotNull"]
/// }
///     </code>
///     <para>The operator names are lowercase kebab identifiers matching the
///     kubb plugin's <c>FilterOperatorName</c> union; the kubb serializer
///     maps them to the DSL symbols (<c>==</c>, <c>~</c>, etc.).</para>
/// </remarks>
/// <remarks>Constructs the transformer with the entity registry it queries for fields.</remarks>
/// <param name="registry">Registry of filterable entities indexed by CLR full name.</param>
public sealed class FilterableSchemaTransformer(FilterableEntityRegistry registry) : IOpenApiSchemaTransformer
{

    /// <summary>Adds <c>x-filterable</c> + <c>x-sortable</c> extensions when the schema is a registered filterable entity.</summary>
    /// <param name="schema">OpenAPI schema being processed.</param>
    /// <param name="context">Transformer context (CLR type info is not exposed in 10.0.9).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        // Only object schemas carry filter field metadata — primitives and
        // arrays are skipped. OpenApiSchema.Type returns null/empty for
        // refs, which we also skip.
        if (schema.Type is not JsonSchemaType.Object)
        {
            return Task.CompletedTask;
        }

        // Resolve CLR type from x-plexor-type. Wire format: full CLR name
        // (Namespace.TypeName) so the registry key lookup is unambiguous
        // across modules.
        if (TryGetExtension(schema, "x-plexor-type") is not { } typeName)
        {
            return Task.CompletedTask;
        }

        if (registry.TryGet(typeName) is not { } fields)
        {
            return Task.CompletedTask;
        }

        // Build x-filterable — only fields with at least one allowed operator.
        // x-sortable receives every field name (parser falls back gracefully).
        var filterable = new JsonArray();
        var sortable = new JsonArray();

        foreach (var field in fields)
        {
            var operators = field.Operators.NamesFor();
            if (operators.Count == 0)
            {
                continue;
            }

            var entry = new JsonObject
            {
                ["name"] = field.Name,
                ["type"] = MapOpenApiType(field.ValueType),
                ["operators"] = new JsonArray(operators.Select(static op => (JsonNode)op).ToArray()),
            };

            filterable.Add(entry);
            sortable.Add((JsonNode)field.Name);
        }

        schema.AddExtension("x-filterable", new JsonNodeExtension(filterable));
        schema.AddExtension("x-sortable", new JsonNodeExtension(sortable));

        return Task.CompletedTask;
    }

    private static string? TryGetExtension(OpenApiSchema schema, string key)
    {
        if (schema.Extensions is null)
        {
            return null;
        }

        if (!schema.Extensions.TryGetValue(key, out var ext))
        {
            return null;
        }

        return ext switch
        {
            JsonNodeExtension json => json.Node switch
            {
                JsonValue value when value.TryGetValue<string>(out var s) => s,
                _ => null,
            },
            _ => null,
        };
    }

    /// <summary>
    ///     Map a CLR property type to its OpenAPI <c>type</c> literal.
    ///     Only the primitives used in filterable entities are covered;
    ///     anything else falls through to <c>"string"</c>, which the kubb
    ///     plugin treats as opaque.
    /// </summary>
    private static string MapOpenApiType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying switch
        {
            { } t when t == typeof(string) => "string",
            { } t when t == typeof(bool) => "boolean",
            { } t when t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) => "integer",
            { } t when t == typeof(float) || t == typeof(double) || t == typeof(decimal) => "number",
            { } t when t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(DateOnly) => "string",
            { } t when t == typeof(Guid) => "string",
            { } t when t.IsEnum => "string",
            _ => "string",
        };
    }
}
