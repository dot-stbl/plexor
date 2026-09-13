// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IRateLimiter — sliding-window rate limiter. Each successful call
// records one rate_limit_events row for the principal AND aggregates
// the org-wide count; the stricter of the two windows wins.
//
// Lives in Plexor.Shared.Kernel because:
//   - The action filter in Plexor.Host depends on the contract (not the
//     EF-backed implementation in Plexor.Modules.Quotas.Infrastructure).
//   - Plexor.Modules.Quotas.Domain (which owns the RateLimitEvent
//     entity) references the kernel, so the discriminator enum
//     RateLimitPrincipalKind also lives in the kernel — one source of
//     truth for "what can a rate limit be applied to".
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Sliding-window rate limiter. Each <see cref="CheckAsync" /> call
///     records one <c>rate_limit_events</c> row for the principal and
///     reads the count over the configured 1-hour window to decide
///     <see cref="RateLimitResult.Allowed" /> /
///     <see cref="RateLimitResult.AllowedWithWarning" /> /
///     <see cref="RateLimitResult.Denied" />.
/// </summary>
/// <remarks>
///     <para><b>Why an interface in the shared kernel.</b> The action
///     filter (<c>RateLimitFilter</c>) lives in the host composition
///     root and depends on the contract — not on the EF-backed
///     implementation in <c>Plexor.Modules.Quotas.Infrastructure</c>.
///     Tests substitute a fake via NSubstitute to assert the filter
///     returns the right ProblemDetails shape without spinning up
///     Postgres.</para>
///     <para><b>Two principals checked.</b> Every call checks the
///     per-principal window (<c>api.requests.per_hour.user</c>) AND the
///     per-org aggregate window (<c>api.requests.per_hour.org</c>). The
///     stricter of the two wins — a runaway service account can be
///     blocked by its org's limit even when its own limit is high.</para>
///     <para><b>Append-mostly.</b> The call records the request
///     regardless of the decision — the row is the proof of the request,
///     and the cleanup BackgroundService (<c>RateLimitCleanupService</c>)
///     deletes rows older than the max window to keep the table bounded.</para>
/// </remarks>
public interface IRateLimiter
{
    /// <summary>
    ///     Check the per-principal sliding window AND the per-org aggregate
    ///     window. Records one <c>rate_limit_events</c> row per call.
    ///     The stricter of the two windows wins.
    /// </summary>
    /// <param name="principal">Caller identity (user id or API key id) plus
    /// the org id the caller belongs to.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    ///     <see cref="RateLimitResult.Allowed" /> when both windows are
    ///     below the warning threshold;
    ///     <see cref="RateLimitResult.AllowedWithWarning" /> when one of
    ///     the windows has crossed the 80% threshold but neither is at
    ///     the cap;
    ///     <see cref="RateLimitResult.Denied" /> when either window is at
    ///     or above the effective cap (carries the <see cref="TimeSpan" />
    ///     until the oldest event in the limiting window ages out).
    /// </returns>
    public Task<RateLimitResult> CheckAsync(
        RateLimitPrincipal principal,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Caller identity carried into <see cref="IRateLimiter.CheckAsync" />.
///     Holds both the principal id (user or API key) and the org id the
///     caller belongs to — the limiter checks the per-principal AND the
///     per-org windows in a single call.
/// </summary>
/// <param name="PrincipalId">User id (JWT auth) or API key id
/// (service auth). For an anonymous caller the filter never reaches the
/// limiter; this id is always populated.</param>
/// <param name="Kind">Discriminator for analytics + the
/// <c>principal_kind</c> column on the <c>rate_limit_events</c> row.</param>
/// <param name="OrgId">Organization the caller belongs to. The limiter
/// uses this for the per-org aggregate window
/// (<c>api.requests.per_hour.org</c>).</param>
public sealed record RateLimitPrincipal(
    Guid PrincipalId,
    RateLimitPrincipalKind Kind,
    Guid OrgId);

/// <summary>
///     Outcome of an <see cref="IRateLimiter.CheckAsync" /> call.
///     Discriminated union with three variants — the action filter
///     pattern-matches on the concrete type to decide the response
///     shape (success, warning header, or 429).
/// </summary>
/// <remarks>
///     <para><b>Tiered enforcement.</b> Same shape as
///     <see cref="QuotaCheckResult" />: 80% threshold fires
///     <see cref="AllowedWithWarning" />; 100% fires
///     <see cref="Denied" />. The two subsystems stay shape-compatible
///     so the filter pipeline can share plumbing.</para>
/// </remarks>
public abstract record RateLimitResult
{
    private RateLimitResult() { }

    /// <summary>
    ///     The request fits inside both the principal and the org window,
    ///     and neither has crossed the warning threshold. The action
    ///     filter passes the request through unchanged.
    /// </summary>
    /// <param name="Remaining">Estimated remaining requests in the
    /// tighter of the two windows (informational; not yet emitted as a
    /// response header — the warning variant is the one that surfaces
    /// metrics to clients).</param>
    public sealed record Allowed(int Remaining) : RateLimitResult;

    /// <summary>
    ///     The request fits, but one of the windows has crossed the 80%
    ///     threshold. The action filter adds an <c>X-Quota-Warning: true</c>
    ///     header to the response so the client UI can surface a warning
    ///     badge.
    /// </summary>
    /// <param name="Remaining">Estimated remaining requests in the
    /// tighter of the two windows.</param>
    /// <param name="Limit">Effective limit of the tighter window.</param>
    public sealed record AllowedWithWarning(int Remaining, int Limit) : RateLimitResult;

    /// <summary>
    ///     One of the two windows is at or above its effective cap. The
    ///     action filter short-circuits with HTTP 429 ProblemDetails
    ///     (<c>code = "rate_limit.exceeded"</c>) and a <c>Retry-After</c>
    ///     header carrying <see cref="RetryAfter" /> in seconds.
    /// </summary>
    /// <param name="Limit">Effective limit of the window that fired the
    /// denial.</param>
    /// <param name="RetryAfter">Time until the oldest in-window event
    /// ages out — the moment a new slot opens up.</param>
    /// <param name="Scope">Which window fired the denial: <c>"principal"</c>
    /// or <c>"org"</c>. The action filter surfaces this in the
    /// ProblemDetails <c>detail</c> field.</param>
    public sealed record Denied(int Limit, TimeSpan RetryAfter, string Scope) : RateLimitResult;
}
