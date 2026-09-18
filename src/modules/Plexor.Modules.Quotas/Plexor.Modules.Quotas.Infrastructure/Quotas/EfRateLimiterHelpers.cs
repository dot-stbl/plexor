// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfRateLimiterHelpers — file-static helpers pulled out of
// EfRateLimiter.cs to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a). The three methods here are:
//   * CountEventsInWindowAsync — COUNT(*) for a given principal or org
//     inside the 1h sliding window (the limiter's step 1 / step 2).
//   * InsertEventAsync — single-row INSERT into rate_limit_events
//     (the limiter's step 3).
//   * OldestInWindowAsync — MIN(occurred_at) for the denied scope, used
//     to compute RetryAfter (the limiter's step 7).
//   * PrincipalKindString — lowercase enum name for the Postgres
//     varchar(16) column; centralised so the wire form stays identical
//     across the INSERT and the entity configuration.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Helpers for <see cref="EfRateLimiter" /> — pulled out so the
///     limiter stays free of private methods
///     (class-layout-and-tooling.md §1a).
/// </summary>
internal static class EfRateLimiterHelpers
{
    /// <summary>
    ///     <c>SELECT COUNT(*) FROM quotas.rate_limit_events WHERE
    ///     principal_id = $1 AND occurred_at &gt; now() - interval '1
    ///     hour'</c> — the per-principal sliding-window count.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the
    /// window boundary.</param>
    /// <param name="principalId">User id or API key id.</param>
    /// <param name="window">Window length (1h for v1).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task<int> CountEventsInWindowAsync(
        QuotasDbContext db,
        TimeProvider clock,
        Guid principalId,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - window;
        var counts = await db.Database
            .SqlQueryRaw<int>(
                """
SELECT COUNT(*)::int AS "Value"
                  FROM quotas.rate_limit_events
                  WHERE principal_id = {0}
                    AND occurred_at > {1}
""",
                principalId,
                cutoff)
            .ToListAsync(cancellationToken);

        return counts.Single();
    }

    /// <summary>
    ///     <c>SELECT COUNT(*) FROM quotas.rate_limit_events WHERE
    ///     org_id = $1 AND occurred_at &gt; now() - interval '1 hour'</c>
    ///     — the per-org aggregate sliding-window count.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the
    /// window boundary.</param>
    /// <param name="orgId">Organization id.</param>
    /// <param name="window">Window length (1h for v1).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task<int> CountOrgEventsInWindowAsync(
        QuotasDbContext db,
        TimeProvider clock,
        Guid orgId,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - window;
        var counts = await db.Database
            .SqlQueryRaw<int>(
                """
SELECT COUNT(*)::int AS "Value"
                  FROM quotas.rate_limit_events
                  WHERE org_id = {0}
                    AND occurred_at > {1}
""",
                orgId,
                cutoff)
            .ToListAsync(cancellationToken);

        return counts.Single();
    }

    /// <summary>
    ///     Insert a single rate-limit event row. The row is the proof
    ///     of the request — the limiter records it on every call
    ///     regardless of the decision (the cleanup BackgroundService
    ///     trims the table daily).
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for
    /// <c>OccurredAt</c> / <c>CreatedAt</c>.</param>
    /// <param name="principalId">User id or API key id.</param>
    /// <param name="kind">Discriminator (User / ApiKey).</param>
    /// <param name="orgId">Organization id (denormalized).</param>
    /// <param name="endpoint">Request path (bounded cardinality).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task InsertEventAsync(
        QuotasDbContext db,
        TimeProvider clock,
        Guid principalId,
        RateLimitPrincipalKind kind,
        Guid orgId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var row = new RateLimitEvent
        {
            Id = Guid.NewGuid(),
            PrincipalId = principalId,
            PrincipalKind = kind,
            OrgId = orgId,
            Endpoint = endpoint,
            OccurredAt = now,
            CreatedAt = now,
        };
        await db.RateLimitEvents.AddAsync(row, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     <c>SELECT MIN(occurred_at) FROM quotas.rate_limit_events WHERE
    ///     principal_id = $1 AND occurred_at &gt; now() - interval '1
    ///     hour'</c>. Used in step 7 to compute RetryAfter — the limiter
    ///     answers "when does the oldest event in the window age out, so a
    ///     new slot opens up?".
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the
    /// window boundary.</param>
    /// <param name="principalId">User id or API key id.</param>
    /// <param name="window">Window length (1h for v1).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The oldest event timestamp, or <see langword="null" />
    /// when no events exist (caller treats null as "no history → allow").</returns>
    public static async Task<DateTimeOffset?> OldestPrincipalInWindowAsync(
        QuotasDbContext db,
        TimeProvider clock,
        Guid principalId,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - window;
        var values = await db.Database
            .SqlQueryRaw<DateTimeOffset>(
                """
SELECT MIN(occurred_at) AS "Value"
                  FROM quotas.rate_limit_events
                  WHERE principal_id = {0}
                    AND occurred_at > {1}
""",
                principalId,
                cutoff)
            .ToListAsync(cancellationToken);

        return values.Single();
    }

    /// <summary>
    ///     Same as <see cref="OldestPrincipalInWindowAsync" />, but
    ///     indexed by <c>org_id</c> for the org-aggregate scope.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the
    /// window boundary.</param>
    /// <param name="orgId">Organization id.</param>
    /// <param name="window">Window length (1h for v1).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The oldest event timestamp, or <see langword="null" />
    /// when no events exist.</returns>
    public static async Task<DateTimeOffset?> OldestOrgInWindowAsync(
        QuotasDbContext db,
        TimeProvider clock,
        Guid orgId,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - window;
        var values = await db.Database
            .SqlQueryRaw<DateTimeOffset>(
                """
SELECT MIN(occurred_at) AS "Value"
                  FROM quotas.rate_limit_events
                  WHERE org_id = {0}
                    AND occurred_at > {1}
""",
                orgId,
                cutoff)
            .ToListAsync(cancellationToken);

        return values.Single();
    }

    /// <summary>
    ///     Lowercase enum name for the Postgres <c>varchar(16)</c>
    ///     <c>principal_kind</c> column. Centralised so the wire form
    ///     stays identical across the entity configuration and any
    ///     future raw-SQL writes.
    /// </summary>
    /// <param name="kind">The principal kind to render.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="kind" /> is not a known
    ///     <see cref="RateLimitPrincipalKind" /> value.
    /// </exception>
    public static string PrincipalKindString(RateLimitPrincipalKind kind)
    {
        return kind switch
        {
            RateLimitPrincipalKind.User => "user",
            RateLimitPrincipalKind.ApiKey => "api_key",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown RateLimitPrincipalKind"),
        };
    }
}
