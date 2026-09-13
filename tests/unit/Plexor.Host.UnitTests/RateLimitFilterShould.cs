// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Tests for RateLimitFilter — covers the four paths through the action
// filter: anonymous passthrough, Allowed, AllowedWithWarning, Denied.
// The IRateLimiter is mocked via NSubstitute; the production EfRateLimiter
// needs real Postgres (raw SQL the InMemory provider can't translate).
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Plexor.Host.Filters;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Host.UnitTests;

/// <summary>
///     Behavioural tests for <see cref="RateLimitFilter" />. The filter
///     translates the four <see cref="RateLimitResult" /> variants into
///     response shapes; each test pins one variant.
/// </summary>
public sealed class RateLimitFilterShould
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrgId = Guid.NewGuid();

    private static (RateLimitFilter Filter, IRateLimiter Limiter, ICurrentUser User, ActionExecutingContext Context)
        BuildContext()
    {
        var limiter = Substitute.For<IRateLimiter>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(UserId);
        currentUser.TenantId.Returns(OrgId);
        currentUser.IsService.Returns(false);

        var httpContext = new DefaultHttpContext
        {
            Request = { Path = "/api/v1/things" },
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        var filter = new RateLimitFilter(limiter, currentUser);
        var filterContext = new ActionExecutingContext(
            actionContext,
            [filter],
            new Dictionary<string, object?>(),
            new object());

        return (filter, limiter, currentUser, filterContext);
    }

    /// <summary>Given an anonymous caller (UserId == Guid.Empty),
    /// when the filter runs, then the limiter is NOT consulted and
    /// the pipeline continues.</summary>
    [Fact(DisplayName = "Given anonymous caller, when filter runs, then limiter is skipped and pipeline continues")]
    public async Task AnonymousCallerSkipsLimiterAsync()
    {
        var (filter, limiter, currentUser, context) = BuildContext();
        currentUser.UserId.Returns(Guid.Empty);
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeTrue();
        await limiter.DidNotReceive().CheckAsync(Arg.Any<RateLimitPrincipal>(), Arg.Any<CancellationToken>());
        context.Result.ShouldBeNull();
    }

    /// <summary>Given the limiter returns Allowed, when the filter runs,
    /// then the pipeline continues and no headers are set.</summary>
    [Fact(DisplayName = "Given Allowed, when filter runs, then pipeline continues and no warning headers are set")]
    public async Task AllowedPassesThroughAsync()
    {
        var (filter, limiter, _, context) = BuildContext();
        limiter.CheckAsync(Arg.Any<RateLimitPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult.Allowed(Remaining: 42));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeTrue();
        context.Result.ShouldBeNull();
        context.HttpContext.Response.Headers.ContainsKey("X-Quota-Warning").ShouldBeFalse();
        context.HttpContext.Response.Headers.ContainsKey("X-RateLimit-Remaining").ShouldBeFalse();
    }

    /// <summary>Given the limiter returns AllowedWithWarning, when the
    /// filter runs, then the pipeline continues AND the warning headers
    /// are appended.</summary>
    [Fact(DisplayName = "Given AllowedWithWarning, when filter runs, then pipeline continues and X-Quota-Warning + X-RateLimit-Remaining headers are appended")]
    public async Task AllowedWithWarningEmitsHeadersAsync()
    {
        var (filter, limiter, _, context) = BuildContext();
        limiter.CheckAsync(Arg.Any<RateLimitPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult.AllowedWithWarning(Remaining: 7, Limit: 100));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeTrue();
        context.Result.ShouldBeNull();
        context.HttpContext.Response.Headers["X-Quota-Warning"].ToString().ShouldBe("true");
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"].ToString().ShouldBe("7");
    }

    /// <summary>Given the limiter returns Denied, when the filter runs,
    /// then the pipeline is short-circuited with a 429 ProblemDetails
    /// + Retry-After header.</summary>
    [Fact(DisplayName = "Given Denied, when filter runs, then pipeline is short-circuited with 429 ProblemDetails + Retry-After")]
    public async Task DeniedShortCircuitsWithProblemDetailsAsync()
    {
        var (filter, limiter, _, context) = BuildContext();
        limiter.CheckAsync(Arg.Any<RateLimitPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult.Denied(
                Limit: 1000,
                RetryAfter: TimeSpan.FromSeconds(47),
                Scope: "principal"));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldNotBeNull();
        var result = context.Result.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);

        var problem = result.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status429TooManyRequests);
        problem.Title.ShouldBe("Too Many Requests");
        problem.Type.ShouldBe("/errors/rate_limit.exceeded");
        var detail = problem.Detail ?? string.Empty;
        detail.ShouldContain("principal");
        detail.ShouldContain("47");
        problem.Extensions.ShouldContainKey("code");
        problem.Extensions["code"].ShouldBe("rate_limit.exceeded");
        problem.Extensions["limit"].ShouldBe(1000);
        problem.Extensions["scope"].ShouldBe("principal");

        context.HttpContext.Response.Headers["Retry-After"].ToString().ShouldBe("47");
    }

    /// <summary>Given a Denied with a sub-second RetryAfter, when the
    /// filter runs, then Retry-After is rounded up to 1 (clients must
    /// not be told to retry in 0 seconds).</summary>
    [Fact(DisplayName = "Given Denied with sub-second RetryAfter, when filter runs, then Retry-After header is at least 1")]
    public async Task DeniedWithSubSecondRetryAfterRoundsUpToOneAsync()
    {
        var (filter, limiter, _, context) = BuildContext();
        limiter.CheckAsync(Arg.Any<RateLimitPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult.Denied(
                Limit: 100,
                RetryAfter: TimeSpan.FromMilliseconds(300),
                Scope: "org"));

        await filter.OnActionExecutionAsync(context, static () => Task.FromResult<ActionExecutedContext>(null!));

        context.HttpContext.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    /// <summary>Given an IsService caller, when the filter runs, then
    /// the limiter receives an ApiKey principal kind.</summary>
    [Fact(DisplayName = "Given IsService=true, when filter runs, then limiter sees ApiKey principal kind")]
    public async Task IsServiceCallerUsesApiKeyKindAsync()
    {
        var (filter, limiter, currentUser, context) = BuildContext();
        currentUser.IsService.Returns(true);
        limiter.CheckAsync(Arg.Any<RateLimitPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult.Allowed(Remaining: 1));

        await filter.OnActionExecutionAsync(context, static () => Task.FromResult<ActionExecutedContext>(null!));

        await limiter.Received(1).CheckAsync(
            Arg.Is<RateLimitPrincipal>(static p =>
                p.PrincipalId == UserId
                && p.Kind == RateLimitPrincipalKind.ApiKey
                && p.OrgId == OrgId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given a user caller, when the filter runs, then the
    /// limiter receives a User principal kind.</summary>
    [Fact(DisplayName = "Given IsService=false, when filter runs, then limiter sees User principal kind")]
    public async Task HumanCallerUsesUserKindAsync()
    {
        var (filter, limiter, currentUser, context) = BuildContext();
        currentUser.IsService.Returns(false);
        limiter.CheckAsync(Arg.Any<RateLimitPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult.Allowed(Remaining: 1));

        await filter.OnActionExecutionAsync(context, static () => Task.FromResult<ActionExecutedContext>(null!));

        await limiter.Received(1).CheckAsync(
            Arg.Is<RateLimitPrincipal>(static p =>
                p.PrincipalId == UserId
                && p.Kind == RateLimitPrincipalKind.User
                && p.OrgId == OrgId),
            Arg.Any<CancellationToken>());
    }
}
