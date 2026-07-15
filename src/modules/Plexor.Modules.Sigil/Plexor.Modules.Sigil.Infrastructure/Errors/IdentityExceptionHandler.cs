// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentityExceptionHandler — maps IdentityException (Phase 3.5 domain
// errors) to RFC 7807 ProblemDetails responses. Registered globally via
// AddExceptionHandler<T> + AddProblemDetails(); no per-endpoint try/catch.
// ============================================================================

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Sigil.Domain.Errors;

namespace Plexor.Modules.Sigil.Infrastructure.Errors;

/// <summary>
///     ASP.NET Core <see cref="IExceptionHandler" /> that converts
///     <see cref="IdentityException" /> to a status-mapped
///     <see cref="ProblemDetails" /> with the discriminator code as
///     the <c>type</c> URI. Other exceptions pass through (the
///     framework's default 500 handler still runs).
/// </summary>
/// <param name="logger"></param>
/// <remarks>
///     <para><b>Status code mapping.</b> Each
///     <see cref="IdentityExceptions" /> code has a canonical HTTP
///     status — a switch in <see cref="MapStatus" /> keeps the
///     mapping in one place rather than scattered across controllers.</para>
///     <para><b>Type URI shape.</b> The <c>type</c> field becomes
///     <c>/errors/&lt;code&gt;</c> — the same path the kubb-generated
///     TS client reads to discriminate error shapes.</para>
/// </remarks>
public sealed class IdentityExceptionHandler(ILogger<IdentityExceptionHandler> logger)
    : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {

        if (exception is not IdentityException identityEx)
        {
            return false;
        }

        var statusCode = MapStatus(identityEx.Code);
        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Type = $"/errors/{identityEx.Code}",
            Title = identityEx.Code,
            Detail = identityEx.Message,
            Status = statusCode,
            Instance = httpContext.Request.Path,
        };

        logger.LogDebug(
            "Identity exception {Code} mapped to HTTP {Status} for {Path}.",
            identityEx.Code,
            statusCode,
            httpContext.Request.Path);

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    /// <summary>
    ///     Maps an <see cref="IdentityExceptions" /> code to its
    ///     canonical HTTP status. Login-flow codes map to 401/423;
    ///     validation codes to 400; resource conflicts to 409; replay
    ///     to 401.
    /// </summary>
    /// <param name="code"></param>
    private static int MapStatus(string code)
    {
        return code switch
        {
            IdentityExceptions.InvalidCredentials => StatusCodes.Status401Unauthorized,
            IdentityExceptions.AccountLocked => StatusCodes.Status423Locked,
            IdentityExceptions.AccountSuspended => StatusCodes.Status403Forbidden,
            IdentityExceptions.RefreshTokenReplayed => StatusCodes.Status401Unauthorized,
            IdentityExceptions.InvalidApiKey => StatusCodes.Status401Unauthorized,
            IdentityExceptions.InvalidEmail => StatusCodes.Status400BadRequest,
            IdentityExceptions.InvalidPasswordHash => StatusCodes.Status400BadRequest,
            IdentityExceptions.InvalidPermission => StatusCodes.Status400BadRequest,
            IdentityExceptions.ApiKeyPermissionsExceedOwner => StatusCodes.Status400BadRequest,
            IdentityExceptions.SshKeyFingerprintTaken => StatusCodes.Status409Conflict,
            IdentityExceptions.Unknown => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
    }
}
