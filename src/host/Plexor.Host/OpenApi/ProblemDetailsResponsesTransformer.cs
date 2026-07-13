// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ProblemDetailsResponsesTransformer — adds RFC 7807 ProblemDetails
// responses (400/401/403/404/409/500) to every OpenAPI operation that
// doesn't already document them. Per-endpoint [ProducesResponseType] is
// reserved for success shapes (2xx) — error shapes are wired once here.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Plexor.Host.OpenApi;

/// <summary>
///     OpenAPI operation transformer that injects the project's standard
///     ProblemDetails error responses (400 / 401 / 403 / 404 / 409 / 500)
///     into every operation. Skips status codes that the operation has
///     already documented via <c>[ProducesResponseType]</c> so per-action
///     custom responses are not overwritten.
/// </summary>
/// <remarks>
///     <para><b>Why all operations get the same set.</b> The
///     <c>AddProblemDetails()</c> middleware writes ProblemDetails for
///     every unhandled error path — auth challenge, model validation,
///     404 from routing, 500 from unhandled exceptions. Documenting
///     only what each endpoint <em>explicitly</em> returns would leave
///     most error shapes invisible to clients and the kubb codegen.</para>
///     <para><b>Why only 4xx/5xx PD.</b> Success responses carry
///     operation-specific shapes (User list, VM detail, …) that the
///     source generator derives from the controller's return type. Error
///     responses are uniform — every error is a ProblemDetails, period.</para>
///     <para><b>401 / 403 are conditional.</b> If the operation has no
///     <see cref="AuthorizeAttribute" />, no policy can forbid the caller
///     and no challenge is needed; those responses would be misleading
///     in the document. The transformer reads the endpoint metadata to
///     decide.</para>
/// </remarks>
public sealed class ProblemDetailsResponsesTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    ///     Standard error responses that apply to every operation.
    ///     401/403 are conditional on <see cref="AuthorizeAttribute" />
    ///     presence and added in <see cref="TransformAsync" /> only when
    ///     the endpoint is protected.
    /// </summary>
    private static readonly IReadOnlyCollection<(string Status, string Description, OpenApiSchema Schema)> UniversalResponses =
    [
        ("400", "Bad Request — request body or query parameters failed validation.", ProblemDetailsSchemas.Validation()),
        ("404", "Not Found — the requested resource does not exist.", ProblemDetailsSchemas.Base()),
        ("409", "Conflict — the request collides with the current state of the target resource.", ProblemDetailsSchemas.Base()),
        ("500", "Internal Server Error — an unexpected server-side failure occurred.", ProblemDetailsSchemas.Base()),
    ];

    /// <summary>
    ///     401 / 403 are added only when the endpoint carries
    ///     <see cref="AuthorizeAttribute" />. Pre-authorization handlers
    ///     would never emit these responses.
    /// </summary>
    private static readonly (string Status, string Description, OpenApiSchema Schema) Unauthorized =
        ("401", "Unauthorized — missing or invalid authentication credentials.", ProblemDetailsSchemas.Base());

    private static readonly (string Status, string Description, OpenApiSchema Schema) Forbidden =
        ("403", "Forbidden — the authenticated caller lacks the required role or permission.", ProblemDetailsSchemas.Base());

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var requiresAuth = context.Description.ActionDescriptor?.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Any() ?? false;

        AddIfMissing(operation, UniversalResponses);
        if (requiresAuth)
        {
            AddIfMissing(operation, [Unauthorized, Forbidden]);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Adds each status code from <paramref name="responses" /> to the
    ///     operation's response map if it isn't already present. Existing
    ///     per-endpoint <c>[ProducesResponseType]</c> declarations win.
    /// </summary>
    private static void AddIfMissing(
        OpenApiOperation operation,
        IReadOnlyCollection<(string Status, string Description, OpenApiSchema Schema)> responses)
    {
        foreach (var (status, description, schema) in responses)
        {
            if (operation.Responses!.ContainsKey(status))
            {
                continue;
            }

            operation.Responses![status] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/problem+json"] = new() { Schema = schema },
                },
            };
        }
    }
}
