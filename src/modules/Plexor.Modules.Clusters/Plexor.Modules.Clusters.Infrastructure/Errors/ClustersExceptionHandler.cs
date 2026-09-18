// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClustersExceptionHandler — maps ClustersException (Phase 5 domain
// errors) to RFC 7807 ProblemDetails responses. Registered globally
// alongside IdentityExceptionHandler via AddExceptionHandler<T>; no
// per-endpoint try/catch.
// ============================================================================

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Clusters.Domain.Errors;

namespace Plexor.Modules.Clusters.Infrastructure.Errors;

/// <summary>
///     ASP.NET Core <see cref="IExceptionHandler" /> that converts
///     <see cref="ClustersException" /> to a status-mapped
///     <see cref="ProblemDetails" /> with the discriminator code as
///     the <c>type</c> URI.
/// </summary>
/// <param name="logger"></param>
public sealed class ClustersExceptionHandler(ILogger<ClustersExceptionHandler> logger)
    : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {

        // Two exception families map through this handler:
        //   - ClustersException — the canonical domain error
        //   - InvalidWorkloadLifecycleTransitionException — a sibling
        //     hierarchy for state-machine rejections (ClustersException
        //     is sealed, so we couldn't subclass it). Both expose
        //     .Code, so the status-mapping path is shared.
        var code = exception switch
        {
            ClustersException clustersEx => clustersEx.Code,
            InvalidWorkloadLifecycleTransitionException lifecycleEx => lifecycleEx.Code,
            _ => null
        };

        if (code is null)
        {
            return false;
        }

        var statusCode = MapStatus(code);
        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Type = $"/errors/{code}",
            Title = code,
            Detail = exception.Message,
            Status = statusCode,
            Instance = httpContext.Request.Path,
        };

        logger.LogDebug(
            "Clusters exception {Code} mapped to HTTP {Status} for {Path}.",
            code,
            statusCode,
            httpContext.Request.Path);

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    /// <summary>
    ///     Maps a <see cref="ClustersExceptions" /> code to its
    ///     canonical HTTP status. Not-found codes map to 404; name /
    ///     hostname conflicts to 409; join-token failures to 401;
    ///     illegal transitions to 409.
    /// </summary>
    /// <param name="code"></param>
    private static int MapStatus(string code)
    {
        return code switch
        {
            ClustersExceptions.ClusterNotFound => StatusCodes.Status404NotFound,
            ClustersExceptions.NodeNotFound => StatusCodes.Status404NotFound,
            ClustersExceptions.ClusterNameTaken => StatusCodes.Status409Conflict,
            ClustersExceptions.NodeHostnameTaken => StatusCodes.Status409Conflict,
            ClustersExceptions.InvalidJoinToken => StatusCodes.Status401Unauthorized,
            ClustersExceptions.JoinTokenConsumed => StatusCodes.Status409Conflict,
            ClustersExceptions.IllegalStatusTransition => StatusCodes.Status409Conflict,
            ClustersExceptions.InvalidLifecycleTransition => StatusCodes.Status409Conflict,
            ClustersExceptions.ComputeProviderFailed => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status400BadRequest,
        };
    }
}
