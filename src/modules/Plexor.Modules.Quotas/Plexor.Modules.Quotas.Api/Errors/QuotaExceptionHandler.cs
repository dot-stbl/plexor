// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaExceptionHandler — maps QuotaExceededException (thrown by resource-
// create handlers in 4.5.c / 4.5.d when the enforcer returns Denied) to
// an RFC 7807 429 ProblemDetails response. Closes the gap where the
// exception would otherwise surface as a 500 via the global error
// pipeline.
//
// Registered globally via AddExceptionHandler<T> + UseExceptionHandler();
// no per-endpoint try/catch. Sits next to IdentityExceptionHandler /
// ClustersExceptionHandler in the composition root.
// ============================================================================

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Api.Errors;

/// <summary>
///     ASP.NET Core <see cref="IExceptionHandler" /> that converts
///     <see cref="QuotaExceededException" /> to an HTTP 429
///     <see cref="ProblemDetails" />. Other exception types pass through
///     unchanged — the next handler in the pipeline handles them.
/// </summary>
/// <remarks>
///     <para><b>Why this handler exists separately.</b> 4.5.c wires the
///     enforcer into <c>Compute.CreateCluster</c> and
///     <c>Compute.CreateWorkload</c>; both throw
///     <see cref="QuotaExceededException" /> on a denied check. Without
///     this handler the exception bubbles up through the global error
///     pipeline as a 500 — a misleading response to a capacity-exhausted
///     caller. This handler turns it into the contractually correct 429
///     with the enforcer's <c>limit</c> / <c>used</c> / <c>requested</c>
///     numbers as ProblemDetails extensions.</para>
///     <para><b>Extension shape.</b> The three numeric extensions let the
///     dashboard render <c>"100 of 256 vCPU used; requested 16 more"</c>
///     without a follow-up round-trip. The <c>code</c> extension matches
///     the discriminator that controllers and tests branch on.</para>
///     <para><b>Body composition.</b> The body is written via
///     <c>HttpResponse.WriteAsJsonAsync</c> with the default serializer
///     so the response type is <c>application/problem+json</c> —
///     consistent with the project's other exception handlers
///     (Identity, Clusters).</para>
/// </remarks>
public sealed class QuotaExceptionHandler() : IExceptionHandler
{
    /// <summary>
    ///     Stable error code rendered into the ProblemDetails
    ///     <c>code</c> extension. Matches
    ///     <see cref="QuotaExceptions.Exceeded" />.
    /// </summary>
    private const string ExceededCode = "quotas.exceeded";

    /// <summary>
    ///     ProblemDetails <c>type</c> URI for quota-exceeded errors.
    ///     The TS client (kubb-generated) discriminates on this URI.
    /// </summary>
    private const string ExceededTypeUri = "/errors/quotas.exceeded";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {

        if (exception is not QuotaExceededException exceeded)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var problem = new ProblemDetails
        {
            Type = ExceededTypeUri,
            Title = "Quota exceeded",
            Detail = "The request would exceed the configured quota for this scope.",
            Status = StatusCodes.Status429TooManyRequests,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["code"] = ExceededCode,
                ["limit"] = exceeded.Limit,
                ["used"] = exceeded.Used,
                ["requested"] = exceeded.Requested,
            },
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
