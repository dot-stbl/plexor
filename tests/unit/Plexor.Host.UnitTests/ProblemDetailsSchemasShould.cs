// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Tests for ProblemDetailsSchemas — verifies the RFC 7807 inline schemas
// used by ProblemDetailsResponsesTransformer have the right shape. The
// transformer itself depends on internal framework context; the schema
// factories are the testable surface.
// ============================================================================

using Microsoft.OpenApi;
using Plexor.Host.OpenApi;
using Shouldly;
using Xunit;

namespace Plexor.Host.UnitTests;

/// <summary>
///     Verifies that <see cref="ProblemDetailsSchemas" /> produces
///     RFC 7807 + ValidationProblemDetails inline schemas matching the
///     contracts the .NET runtime emits.
/// </summary>
public sealed class ProblemDetailsSchemasShould
{
    /// <summary>Verifies the base ProblemDetails schema carries all six
    /// spec fields plus an <c>extensions</c> catch-all for framework
    /// metadata (traceId, requestId, etc.).</summary>
    [Fact(DisplayName = "Base schema exposes type/title/status/detail/instance/extensions")]
    public void BaseSchemaExposesRfc7807Fields()
    {
        var schema = ProblemDetailsSchemas.Base();

        schema.Type.ShouldBe(JsonSchemaType.Object);
        schema.Properties.ShouldNotBeNull();
        schema.Properties!.Keys.ShouldContain("type");
        schema.Properties.Keys.ShouldContain("title");
        schema.Properties.Keys.ShouldContain("status");
        schema.Properties.Keys.ShouldContain("detail");
        schema.Properties.Keys.ShouldContain("instance");
        schema.Properties.Keys.ShouldContain("extensions");
    }

    /// <summary>Verifies the <c>status</c> field is declared as an
    /// int32 — matches the .NET <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails.Status" />
    /// property type.</summary>
    [Fact(DisplayName = "Status field is declared as int32")]
    public void StatusFieldIsInt32()
    {
        var schema = ProblemDetailsSchemas.Base();

        var status = schema.Properties!["status"];
        status.Type.ShouldBe(JsonSchemaType.Integer);
        status.Format.ShouldBe("int32");
    }

    /// <summary>Verifies the <c>extensions</c> field accepts arbitrary
    /// keys so framework-injected metadata (traceId, correlationId)
    /// round-trips through the JSON serializer.</summary>
    [Fact(DisplayName = "Extensions field has additionalProperties: true")]
    public void ExtensionsFieldIsOpenMap()
    {
        var schema = ProblemDetailsSchemas.Base();

        var extensions = schema.Properties!["extensions"];
        extensions.AdditionalProperties.ShouldNotBeNull();
    }

    /// <summary>Verifies ValidationProblemDetails wraps the base schema
    /// via allOf and adds an <c>errors</c> dictionary whose values are
    /// arrays of strings.</summary>
    [Fact(DisplayName = "Validation schema adds errors dictionary via allOf")]
    public void ValidationSchemaWrapsBaseViaAllOfAndAddsErrors()
    {
        var schema = ProblemDetailsSchemas.Validation();

        schema.AllOf.ShouldNotBeNull();
        schema.AllOf!.Count.ShouldBe(2);

        // The second allOf segment is the ValidationProblemDetails-specific
        // errors dictionary.
        var errorsSegment = schema.AllOf[1];
        errorsSegment.Properties.ShouldNotBeNull();
        errorsSegment.Properties!.Keys.ShouldContain("errors");

        var errors = errorsSegment.Properties["errors"];
        errors.Type.ShouldBe(JsonSchemaType.Object);
        errors.AdditionalProperties!.Type.ShouldBe(JsonSchemaType.Array);
        errors.AdditionalProperties.Items!.Type.ShouldBe(JsonSchemaType.String);
    }

    /// <summary>Verifies each factory call returns a fresh instance —
    /// callers may mutate the result without affecting subsequent calls.</summary>
    [Fact(DisplayName = "Each factory call returns a new instance")]
    public void FactoryReturnsFreshInstance()
    {
        var first = ProblemDetailsSchemas.Base();
        var second = ProblemDetailsSchemas.Base();

        ReferenceEquals(first, second).ShouldBeFalse();
    }
}
