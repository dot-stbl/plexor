// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaExceptionHandlerShould — exercise the 4.5.g.1
// QuotaExceptionHandler in isolation. Verifies that
// QuotaExceededException maps to an HTTP 429 ProblemDetails body with
// the expected discriminator code + numeric extensions, and that any
// other exception type passes through (returns false from TryHandleAsync
// so the next handler in the pipeline runs).
// ============================================================================

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Plexor.Modules.Quotas.Api.Errors;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Quotas.Unit;

/// <summary>
///     Behavioural tests for <see cref="QuotaExceptionHandler" />.
///     Constructs a fresh <see cref="DefaultHttpContext" /> with an
///     in-memory response body per test so concurrent tests don't share
///     state.
/// </summary>
public sealed class QuotaExceptionHandlerShould
{
    /// <summary>
    ///     Given a <see cref="QuotaExceededException" />, when
    ///     <see cref="QuotaExceptionHandler.TryHandleAsync" /> runs,
    ///     then it returns true, writes status 429, and the body is a
    ///     ProblemDetails document with the <c>code</c> /
    ///     <c>limit</c> / <c>used</c> / <c>requested</c> extensions
    ///     matching the exception.
    /// </summary>
    [Fact(DisplayName = "Given QuotaExceededException, when handled, then response is 429 with code + numeric extensions")]
    public async Task TryHandleAsync_WithQuotaExceededException_Returns429WithExtensionsAsync()
    {
        var handler = new QuotaExceptionHandler();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new QuotaExceededException(limit: 10m, used: 10m, requested: 1m);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        var root = document.RootElement;

        root.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status429TooManyRequests);
        root.GetProperty("title").GetString().ShouldBe("Quota exceeded");
        root.GetProperty("type").GetString().ShouldBe("/errors/quotas.exceeded");
        root.GetProperty("code").GetString().ShouldBe("quotas.exceeded");
        root.GetProperty("limit").GetDecimal().ShouldBe(10m);
        root.GetProperty("used").GetDecimal().ShouldBe(10m);
        root.GetProperty("requested").GetDecimal().ShouldBe(1m);
    }

    /// <summary>
    ///     Given an exception that is not <see cref="QuotaExceededException" />,
    ///     when the handler runs, then it returns false and does not
    ///     mutate the response — the next handler in the pipeline
    ///     handles it (e.g. IdentityExceptionHandler, ClustersExceptionHandler,
    ///     or the global 500 fallback).
    /// </summary>
    [Fact(DisplayName = "Given a non-quota exception, when handled, then handler returns false and leaves response untouched")]
    public async Task TryHandleAsync_WithOtherException_ReturnsFalseAsync()
    {
        var handler = new QuotaExceptionHandler();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("not a quota error"), CancellationToken.None);

        handled.ShouldBeFalse();
        httpContext.Response.StatusCode.ShouldNotBe(StatusCodes.Status429TooManyRequests);
        httpContext.Response.Body.Length.ShouldBe(0);
    }
}
