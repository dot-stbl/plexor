// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RateLimitFilterResultMapper — file-static helpers pulled out of
// RateLimitFilter.cs to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a — IAsyncActionFilter implementations
// delegate the result-shaping helpers to a file-static class instead of
// carrying private methods).
//
// Two methods:
//   * ApplyWarning — sets the X-Quota-Warning + X-RateLimit-Remaining
//     response headers for an AllowedWithWarning outcome.
//   * ApplyDenied — writes the 429 ProblemDetails (RFC 9457) with
//     extensions { code, limit, retryAfter, scope }, sets the
//     Retry-After header (seconds), and short-circuits the action
//     pipeline by setting context.Result.
// ============================================================================

using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Host.Filters;

/// <summary>
///     Response-shaping helpers for <see cref="RateLimitFilter" />.
///     Pulled out so the filter stays free of private methods
///     (class-layout-and-tooling.md §1a).
/// </summary>
internal static class RateLimitFilterResultMapper
{
    /// <summary>Response header signalling the warning variant.</summary>
    public const string QuotaWarningHeader = "X-Quota-Warning";

    /// <summary>Response header carrying the tighter window's
    /// remaining capacity on a warning response.</summary>
    public const string RemainingHeader = "X-RateLimit-Remaining";

    /// <summary>Standard <c>Retry-After</c> header (seconds). Same name
    /// as RFC 9110 §10.2.3 / RFC 6585.</summary>
    public const string RetryAfterHeader = "Retry-After";

    /// <summary>Stable discriminator code for the rate-limit-deny
    /// ProblemDetails <c>extensions.code</c> field.</summary>
    public const string RateLimitExceededCode = "rate_limit.exceeded";

    /// <summary>
    ///     Emit the warning-variant headers on the current response.
    ///     Headers are appended (not set) so the action result can
    ///     add its own <c>Cache-Control</c> / <c>ETag</c> / etc. without
    ///     clobbering the rate-limit signal.
    /// </summary>
    /// <param name="context">Action filter context.</param>
    /// <param name="warning">Warning variant from
    /// <see cref="IRateLimiter.CheckAsync" />.</param>
    public static void ApplyWarning(
        ActionExecutingContext context,
        RateLimitResult.AllowedWithWarning warning)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.Append(QuotaWarningHeader, "true");
        headers.Append(RemainingHeader, warning.Remaining.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     Short-circuit the action pipeline with a 429 ProblemDetails
    ///     carrying the deny's limit + retry-after seconds + scope.
    ///     Sets the standard <c>Retry-After</c> header in seconds
    ///     (ceiling-rounded; a sub-second retry becomes <c>1</c>).
    /// </summary>
    /// <param name="context">Action filter context.</param>
    /// <param name="denied">Denied variant from
    /// <see cref="IRateLimiter.CheckAsync" />.</param>
    public static void ApplyDenied(
        ActionExecutingContext context,
        RateLimitResult.Denied denied)
    {
        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(denied.RetryAfter.TotalSeconds));

        var problem = new ProblemDetails
        {
            Type = $"/errors/{RateLimitExceededCode}",
            Title = "Too Many Requests",
            Detail = $"Rate limit exceeded on {denied.Scope}; retry after {retryAfterSeconds}s.",
            Status = StatusCodes.Status429TooManyRequests,
            Instance = context.HttpContext.Request.Path,
            Extensions =
            {
                ["code"] = RateLimitExceededCode,
                ["limit"] = denied.Limit,
                ["retryAfter"] = retryAfterSeconds,
                ["scope"] = denied.Scope,
            },
        };

        context.HttpContext.Response.Headers.Append(RetryAfterHeader, retryAfterSeconds.ToString(CultureInfo.InvariantCulture));
        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" },
        };
    }
}
