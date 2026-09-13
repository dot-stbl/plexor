// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RateLimitCleanupServiceHelpers — file-static helpers pulled out of
// RateLimitCleanupService.cs to satisfy the no-private-methods
// convention (class-layout-and-tooling.md §1a / §9.3 — the BackgroundService
// override body delegates to file-static helpers, never private methods).
//
// Three helpers:
//   * NextSweepTime — next "minute 5 past the hour" boundary the sweep
//     should fire on. Hourly cadence; the :05 offset puts the sweep
//     after the typical API-hour rollover.
//   * ComputeRetentionCutoff — now() minus the 2h retention window.
//     2h is one hour more than the longest rate-limit window (1h) so
//     a row that just rolled out of the 1h window is still queryable
//     for the cleanup sweep.
//   * SweepAsync — the actual DELETE. Opens a scope, resolves the
//     scoped QuotasDbContext, runs ExecuteSqlRawAsync against the
//     rate_limit_events table, logs the rowcount.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Quotas.Infrastructure.Persistence;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Helpers for <see cref="RateLimitCleanupService" />. Pulled out
///     so the BackgroundService stays free of private methods
///     (class-layout-and-tooling.md §1a / §9.3).
/// </summary>
internal static class RateLimitCleanupServiceHelpers
{
    /// <summary>
    ///     Next sweep time — the upcoming <c>HH:05:00</c> UTC instant
    ///     (one minute past the typical API-hour rollover, when most of
    ///     the trailing-edge event rows are safely outside the 1h
    ///     window). Always strictly in the future relative to
    ///     <paramref name="clock" />.
    /// </summary>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for
    /// the "now" anchor.</param>
    public static DateTimeOffset NextSweepTime(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var candidate = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            5,
            0,
            TimeSpan.Zero);

        return candidate > now
            ? candidate
            : candidate.AddHours(1);
    }

    /// <summary>
    ///     Cutoff timestamp for the DELETE — anything strictly older
    ///     than this is removed. 2h retention is one hour past the
    ///     longest rate-limit window (1h) so the cleanup never races
    ///     with an active query.
    /// </summary>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for
    /// the "now" anchor.</param>
    /// <param name="retention">Retention window (default 2h).</param>
    public static DateTimeOffset ComputeRetentionCutoff(
        TimeProvider clock,
        TimeSpan retention)
    {
        return clock.GetUtcNow() - retention;
    }

    /// <summary>
    ///     Open a scope, resolve a <see cref="QuotasDbContext" />, and
    ///     run the DELETE. Logs the rowcount for observability.
    /// </summary>
    /// <param name="scopeFactory">Application-level scope factory (the
    /// BackgroundService is singleton; DbContext is scoped).</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for
    /// the cutoff computation.</param>
    /// <param name="retention">Retention window (default 2h).</param>
    /// <param name="logger">Structured logger for the rowcount.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task<int> SweepAsync(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        TimeSpan retention,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotasDbContext>();
        var cutoff = ComputeRetentionCutoff(clock, retention);

        var deleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM quotas.rate_limit_events WHERE occurred_at < {0}",
            [cutoff],
            cancellationToken);

        logger.LogInformation(
            "RateLimitCleanupService: deleted {Deleted} events older than {Cutoff}",
            deleted,
            cutoff);

        return deleted;
    }
}
