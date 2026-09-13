// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RateLimitFilter — IAsyncActionFilter that runs the IRateLimiter on
// every authenticated action. Lives in Plexor.Host (composition root)
// because it crosses module boundaries:
//   * IRateLimiter — Plexor.Modules.Quotas.Infrastructure (scoped, per-request)
//   * ICurrentUser — Plexor.Modules.Sigil.Application.Abstractions (scoped)
//
// MVC filters are registered globally via
// `builder.Services.Configure<MvcOptions>(...)` in Program.cs — every
// action gets the check before the controller runs.
// ============================================================================

using Microsoft.AspNetCore.Mvc.Filters;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Host.Filters;

/// <summary>
///     Runs the sliding-window rate limiter on every authenticated
///     action. Anonymous callers pass through unchanged (the auth
///     middleware runs before the filter and emits 401 for unauthenticated
///     requests; the rate limiter only sees authenticated principals).
/// </summary>
/// <param name="limiter">Scoped <see cref="IRateLimiter" /> — reads
/// the count + inserts one event per call.</param>
/// <param name="currentUser">Scoped <see cref="ICurrentUser" /> —
/// resolves the caller's principal id + tenant id + auth kind.</param>
/// <remarks>
///     <para><b>Why after auth, before the controller.</b> ASP.NET Core
///     runs authentication before authorization before action filters;
///     the filter therefore has access to <see cref="HttpContext.User" />
///     and the populated <see cref="ICurrentUser" />. The controller
///     does not run if the filter short-circuits with a 429.</para>
///     <para><b>Why <c>TenantId</c> not <c>OrgId</c>.</b> The identity
///     module still exposes <c>TenantId</c>; the Quotas module
///     internally uses <c>OrgId</c>. They are the same database
///     concept — the rename is in progress. The filter passes the
///     tenant id through to the limiter's <see cref="RateLimitPrincipal.OrgId" />
///     as-is.</para>
///     <para><b>Header surface.</b> The filter emits:
///     <c>X-Quota-Warning: true</c> + <c>X-RateLimit-Remaining</c> on
///     warning responses; <c>Retry-After</c> (seconds) on denied
///     responses. The header names mirror the de-facto convention
///     (GitHub / Stripe / AWS) so client libraries don't need a
///     Plexor-specific decoder.</para>
/// </remarks>
public sealed class RateLimitFilter(
    IRateLimiter limiter,
    ICurrentUser currentUser) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Anonymous passthrough. The auth middleware returns 401
        // before us; if we land here with an empty principal, the
        // caller is one we don't want to meter (and we couldn't
        // anyway — the rate limit windows are keyed on caller id).
        if (currentUser.UserId == Guid.Empty)
        {
            await next();
            return;
        }

        var principal = new RateLimitPrincipal(
            PrincipalId: currentUser.UserId,
            Kind: currentUser.IsService
                ? RateLimitPrincipalKind.ApiKey
                : RateLimitPrincipalKind.User,
            OrgId: currentUser.TenantId);

        var result = await limiter.CheckAsync(principal, context.HttpContext.RequestAborted);

        switch (result)
        {
            case RateLimitResult.Denied denied:
                RateLimitFilterResultMapper.ApplyDenied(context, denied);
                return;

            case RateLimitResult.AllowedWithWarning warning:
                RateLimitFilterResultMapper.ApplyWarning(context, warning);
                break;

            case RateLimitResult.Allowed:
                break;
        }

        await next();
    }
}
