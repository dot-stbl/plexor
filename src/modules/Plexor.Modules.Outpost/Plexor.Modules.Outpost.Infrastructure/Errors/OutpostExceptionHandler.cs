// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostExceptionHandler — IExceptionHandler that maps OutpostException
// to a ProblemDetails response. Coexists with the per-module handlers
// (IdentityExceptionHandler, ClustersExceptionHandler, QuotaExceptionHandler);
// each owns its own typed exception + stable Code.
//
// The handler reads the Code from the exception and exposes it on the
// ProblemDetails `code` extension. Status code defaults to 400 (the
// most common client error); specific Codes can opt into a different
// status (InvalidJoinToken → 401, ClusterNotFound → 404, etc.).
// ============================================================================

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Outpost.Application;

namespace Plexor.Modules.Outpost.Infrastructure.Errors;

/// <summary>
///     Maps <see cref="OutpostException" /> to a ProblemDetails
///     response. Registered via <c>AddExceptionHandler&lt;...&gt;</c>
///     in the composition root.
/// </summary>
/// <param name="logger">Logger — critical-level sink for handler failures.</param>
public sealed class OutpostExceptionHandler(ILogger<OutpostExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not OutpostException outpostException)
        {
            return false;
        }

        logger.LogWarning(
            exception,
            "Outpost: {Code} — {Message}",
            outpostException.Code,
            outpostException.Message);

        var statusCode = StatusCodeFor(outpostException.Code);
        httpContext.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = TitleFor(outpostException.Code),
            Detail = outpostException.Message,
            Type = $"https://plexor.example.com/errors/{outpostException.Code}",
        };
        problem.Extensions["code"] = outpostException.Code;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static int StatusCodeFor(string code)
    {
        return code switch
        {
            OutpostExceptions.InvalidJoinToken => StatusCodes.Status401Unauthorized,
            OutpostExceptions.NodeNotFound => StatusCodes.Status404NotFound,
            OutpostExceptions.ClusterNotFound => StatusCodes.Status404NotFound,
            OutpostExceptions.NodeHostnameTaken => StatusCodes.Status409Conflict,
            OutpostExceptions.NodeRoleMismatch => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
    }

    private static string TitleFor(string code)
    {
        return code switch
        {
            OutpostExceptions.InvalidJoinToken => "Invalid join token",
            OutpostExceptions.NodeNotFound => "Node not found",
            OutpostExceptions.ClusterNotFound => "Cluster not found",
            OutpostExceptions.NodeHostnameTaken => "Hostname already taken",
            OutpostExceptions.NodeRoleMismatch => "Role mismatch",
            _ => "Outpost request rejected",
        };
    }
}