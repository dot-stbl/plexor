// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfRateLimiter — sliding-window rate limiter. Per call:
//   1. SELECT COUNT(*) for principal_id in the 1h window
//   2. SELECT COUNT(*) for org_id in the 1h window
//   3. INSERT one rate_limit_events row
//   4. Resolve effective limits for both api.requests.per_hour.user
//      and api.requests.per_hour.org
//   5. Decide Allowed / AllowedWithWarning (80%) / Denied
//
// The two COUNTs + the INSERT + the two resolver calls do not share a
// transaction. A tiny race window exists between the COUNT and the
// INSERT — two concurrent callers at the cap could both observe
// count == limit and both insert, briefly tipping the table to limit+2.
// The cleanup BackgroundService trims old rows; the limit is a
// soft-suggestion, not a hard guarantee (matching the spec's "no admin
// override" philosophy).
// ============================================================================

using Microsoft.Extensions.Logging;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     EF-backed <see cref="IRateLimiter" />. Sliding window via
///     <c>SELECT COUNT(*) + INSERT</c> against the
///     <c>quotas.rate_limit_events</c> append-mostly table; both
///     indexes (<c>(principal_id, occurred_at)</c> and
///     <c>(org_id, occurred_at)</c>) cover the two read paths.
/// </summary>
/// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
/// <param name="resolver">Scoped <see cref="IQuotaScopeResolver" />
/// the limiter queries twice — once for
/// <c>api.requests.per_hour.user</c>, once for
/// <c>api.requests.per_hour.org</c>.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// window boundary + the event row's <c>OccurredAt</c> stamp.</param>
/// <param name="logger">Structured logger for the deny path.</param>
/// <remarks>
///     <para><b>Why raw SQL.</b> EF Core's
///     <c>Database.SqlQueryRaw&lt;T&gt;</c> is the canonical escape hatch
///     for scalar projections off tables the context does not track. The
///     <c>rate_limit_events</c> table is append-mostly and never queried
///     back via the EF model on the request hot path — the column types
///     match the entity config exactly, so the raw-SQL result row
///     materialises without translation.</para>
///     <para><b>Scope always <c>QuotaScope.Org(orgId)</c>.</b> Rate
///     limits are org-aggregate concepts — the resolver walks folder →
///     org → default for the org's id, and a folder-scoped override
///     applies to the folder only. Per-folder rate-limit assignments
///     are a Phase 2 extension once the folder/team walker carries the
///     team hint.</para>
///     <para><b>Why no transaction.</b> The rate-limit INSERT is
///     independent of any resource-create the controller might perform.
///     A failed resource create would not roll back the rate-limit
///     event, which is correct — the request still hit the API. The
///     quota enforcer's <c>CheckAndReserveAsync</c> does share a
///     transaction with the resource INSERT because that's a different
///     guarantee ("don't over-reserve").</para>
/// </remarks>
internal sealed class EfRateLimiter(
    QuotasDbContext db,
    IQuotaScopeResolver resolver,
    TimeProvider clock,
    ILogger<EfRateLimiter> logger) : IRateLimiter
{
    /// <summary>1h sliding window (matches the catalog
    /// <c>api.requests.per_hour.*</c> definitions).</summary>
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>80% threshold fires the warning variant of the result.</summary>
    private const decimal WarningThresholdPct = 80m;

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckAsync(
        RateLimitPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        // Step 1 + 2 — count events in window for both scopes. Run in
        // parallel — both queries hit a btree index, both are bounded
        // by the cleanup window (1h retention), and the savings add up
        // on a hot API path.
        var principalCountTask = EfRateLimiterHelpers.CountEventsInWindowAsync(
            db, clock, principal.PrincipalId, Window, cancellationToken);
        var orgCountTask = EfRateLimiterHelpers.CountOrgEventsInWindowAsync(
            db, clock, principal.OrgId, Window, cancellationToken);
        await Task.WhenAll(principalCountTask, orgCountTask);
        var principalCount = await principalCountTask;
        var orgCount = await orgCountTask;

        // Step 4 — resolve the effective limits. Both queries run
        // against QuotaScope.Org(orgId); the resolver walks folder →
        // org → default. Run in parallel.
        var scope = QuotaScope.Org(principal.OrgId);
        var principalLimitTask = resolver.ResolveAsync(
            scope, QuotaDefinitionKey.ApiRequestsPerHourUser, cancellationToken);
        var orgLimitTask = resolver.ResolveAsync(
            scope, QuotaDefinitionKey.ApiRequestsPerHourOrg, cancellationToken);
        await Task.WhenAll(principalLimitTask, orgLimitTask);
        var principalLimit = (await principalLimitTask)?.Value;
        var orgLimit = (await orgLimitTask)?.Value;

        // Step 3 — insert the rate-limit event row. Done before the
        // decision so the table reflects the actual request regardless
        // of whether the request is denied (the row is the proof of
        // the request — analytics use it to flag runaway callers).
        await EfRateLimiterHelpers.InsertEventAsync(
            db,
            clock,
            principal.PrincipalId,
            principal.Kind,
            principal.OrgId,
            string.Empty,
            cancellationToken);

        // Step 7 — Denied when either count has reached the cap.
        // A null limit means "unlimited" — the catalog has no row + no
        // assignment; the limiter treats that as no cap.
        var principalOver = principalLimit is { } pLimit && principalCount >= pLimit;
        var orgOver = orgLimit is { } oLimit && orgCount >= oLimit;
        if (principalOver || orgOver)
        {
            var deniedScope = principalOver ? "principal" : "org";
            var deniedId = principalOver ? principal.PrincipalId : principal.OrgId;
            var deniedLimit = principalOver
                ? (int)principalLimit!.Value
                : (int)orgLimit!.Value;

            var oldest = principalOver
                ? await EfRateLimiterHelpers.OldestPrincipalInWindowAsync(
                    db, clock, deniedId, Window, cancellationToken)
                : await EfRateLimiterHelpers.OldestOrgInWindowAsync(
                    db, clock, deniedId, Window, cancellationToken);

            var retryAfter = oldest is { } oldestAt
                ? oldestAt + Window - clock.GetUtcNow()
                : TimeSpan.Zero;

            // Floor to zero — a stale clock or a freshly-vacated
            // window can produce a negative span; the filter still
            // emits Retry-After: 0 instead of throwing on Math.Ceiling.
            if (retryAfter < TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
            }

            logger.LogWarning(
                "Rate limit denied for {Scope} {Id} on org {OrgId}: count {Count}, limit {Limit}, retry after {RetryAfterSeconds}s",
                deniedScope,
                deniedId,
                principal.OrgId,
                principalOver ? principalCount : orgCount,
                deniedLimit,
                (int)Math.Ceiling(retryAfter.TotalSeconds));

            return new RateLimitResult.Denied(
                Limit: deniedLimit,
                RetryAfter: retryAfter,
                Scope: deniedScope);
        }

        // Step 8 — AllowedWithWarning at the 80% threshold.
        // Either window can fire; the tighter of the two is what the
        // filter surfaces (highest ratio of used/cap).
        var principalPct = ComputeUsagePct(principalCount, principalLimit);
        var orgPct = ComputeUsagePct(orgCount, orgLimit);
        const decimal warningRatio = WarningThresholdPct / 100m;
        var principalWarning = principalPct > warningRatio;
        var orgWarning = orgPct > warningRatio;
        if (principalWarning || orgWarning)
        {
            var (used, cap) = principalWarning
                ? (principalCount, (int)principalLimit!.Value)
                : (orgCount, (int)orgLimit!.Value);
            var remaining = Math.Max(0, cap - used);

            return new RateLimitResult.AllowedWithWarning(
                Remaining: remaining,
                Limit: cap);
        }

        // Step 9 — Allowed. Report the tighter window's remaining count.
        var allowedRemaining = ComputeRemaining(principalCount, principalLimit, orgCount, orgLimit);
        return new RateLimitResult.Allowed(Remaining: allowedRemaining);
    }

    /// <summary>
    ///     Compute <c>used / cap</c> as a ratio; returns zero when the
    ///     limit is null (unlimited) or zero.
    /// </summary>
    /// <param name="used"></param>
    /// <param name="limit"></param>
    private static decimal ComputeUsagePct(int used, decimal? limit)
    {
        if (limit is not { } cap || cap <= 0m)
        {
            return 0m;
        }

        return used / cap;
    }

    /// <summary>
    ///     Pick the tighter window (lowest remaining count) and report
    ///     its remaining capacity. Returns zero when either window is
    ///     unlimited (null limit).
    /// </summary>
    /// <param name="principalCount"></param>
    /// <param name="principalLimit"></param>
    /// <param name="orgCount"></param>
    /// <param name="orgLimit"></param>
    private static int ComputeRemaining(
        int principalCount,
        decimal? principalLimit,
        int orgCount,
        decimal? orgLimit)
    {
        var principalRemaining = principalLimit is { } pLimit
            ? Math.Max(0, (int)pLimit - principalCount)
            : int.MaxValue;
        var orgRemaining = orgLimit is { } oLimit
            ? Math.Max(0, (int)oLimit - orgCount)
            : int.MaxValue;

        return Math.Min(principalRemaining, orgRemaining);
    }
}
