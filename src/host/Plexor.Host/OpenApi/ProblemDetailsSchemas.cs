// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ProblemDetailsSchemas — single source of truth for the RFC 7807 schemas
// embedded in the OpenAPI document. Reused by
// ProblemDetailsResponsesTransformer so every 4xx/5xx response points at
// the same $ref. ValidationProblemDetails references ProblemDetails via
// allOf and adds an `errors` map per the MVC convention.
// ============================================================================

using Microsoft.OpenApi;

namespace Plexor.Host.OpenApi;

/// <summary>
///     RFC 7807 + ValidationProblemDetails schemas shared by every
///     4xx/5xx response. Static factory methods return new
///     <see cref="OpenApiSchema" /> instances so callers can mutate
///     without affecting each other.
/// </summary>
public static class ProblemDetailsSchemas
{
    /// <summary>
    ///     RFC 7807 ProblemDetails base shape. The six spec fields
    ///     (<c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>,
    ///     <c>instance</c>) plus an <c>extensions</c> catch-all for
    ///     <c>traceId</c> and similar framework-injected metadata.
    /// </summary>
    public static OpenApiSchema Base()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["extensions"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    AdditionalProperties = new OpenApiSchema(),
                },
            },
        };
    }

    /// <summary>
    ///     ASP.NET Core's <c>ValidationProblemDetails</c> — extends
    ///     <see cref="Base" /> with an <c>errors</c> dictionary whose
    ///     values are arrays of strings (one per field-level message).
    ///     Modeled via <c>allOf</c> with the base schema.
    /// </summary>
    public static OpenApiSchema Validation()
    {
        return new OpenApiSchema
        {
            AllOf =
            [
                Base(),
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                    {
                        ["errors"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            AdditionalProperties = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Array,
                                Items = new OpenApiSchema { Type = JsonSchemaType.String },
                            },
                        },
                    },
                },
            ],
        };
    }
}
