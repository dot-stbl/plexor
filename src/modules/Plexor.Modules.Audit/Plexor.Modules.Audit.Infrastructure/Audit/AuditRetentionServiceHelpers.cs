// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditRetentionServiceHelpers — file-static helpers pulled out of
// AuditRetentionService.cs to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a / §9.3 — the BackgroundService
// override body delegates to file-static helpers, never private methods).
//
// Three helpers:
//   * NextSweepTime — next "SweepHourUtc:00:00" UTC instant the sweep
//     should fire on. Configurable anchor (default 05:00 UTC) puts the
//     sweep in the operator-quiet window.
//   * ComputeRetentionCutoff — now() minus RetentionDays. Any audit
//     row with occurred_at strictly less than this is removed.
//   * SweepAsync — the actual DELETE in chunks. Takes an already-
//     resolved AuditDbContext so production (BackgroundService opens a
//     scope) + tests (unit test creates an in-memory context) can both
//     share the same body.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Audit.Application.Audit;
using Plexor.Modules.Audit.Domain.Entities;
using Plexor.Modules.Audit.Infrastructure.Persistence;

namespace Plexor.Modules.Audit.Infrastructure.Audit;

/// <summary>
///     Helpers for <see cref="AuditRetentionService" />. Pulled out
///     so the BackgroundService stays free of private methods
///     (class-layout-and-tooling.md §1a / §9.3).
/// </summary>
internal static class AuditRetentionServiceHelpers
{
    /// <summary>
    ///     Next sweep time — the upcoming
    ///     <c>SweepHourUtc:00:00.000</c> UTC instant. Always strictly
    ///     in the future relative to <paramref name="clock" />.
    /// </summary>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for
    /// the "now" anchor.</param>
    /// <param name="sweepHourUtc">Hour-of-day offset (0-23).</param>
    public static DateTimeOffset NextSweepTime(TimeProvider clock, int sweepHourUtc)
    {
        var now = clock.GetUtcNow();
        var candidate = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            sweepHourUtc,
            0,
            0,
            TimeSpan.Zero);

        return candidate > now
            ? candidate
            : candidate.AddDays(1);
    }

    /// <summary>
    ///     Cutoff timestamp for the DELETE — anything strictly older
    ///     than this is removed. The retention window is
    ///     <paramref name="options" />.RetentionDays back from
    ///     <paramref name="clock" />.
    /// </summary>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for
    /// the "now" anchor.</param>
    /// <param name="options">Bound audit options (retention window).</param>
    public static DateTimeOffset ComputeRetentionCutoff(
        TimeProvider clock,
        AuditOptions options)
    {
        return clock.GetUtcNow() - TimeSpan.FromDays(options.RetentionDays);
    }

    /// <summary>
    ///     Run the DELETE in chunks of <c>options.BatchSize</c> rows
    ///     against the supplied <see cref="AuditDbContext" />. Bounded
    ///     transactions keep the lock window small even when the
    ///     retention sweep hits a high-volume tenant. Returns the
    ///     total rows deleted across all batches.
    /// </summary>
    /// <remarks>
    ///     <para><b>Why <c>Take + RemoveRange + SaveChangesAsync</c>
    ///     instead of <c>ExecuteDeleteAsync</c> or raw SQL.</b>
    ///     The chunked tracked-entity path is cross-provider portable
    ///     — it works on the in-memory provider the unit-test project
    ///     uses + Postgres in production. <c>ExecuteDeleteAsync</c>
    ///     would be a single SQL statement and lock-friendly at scale,
    ///     but it doesn't translate to the in-memory provider the
    ///     retention tests pin; raw SQL also doesn't. The tracked-
    ///     entity path costs one SELECT + one DELETE per batch, which
    ///     is acceptable for a daily sweep that only fires once per
    ///     <c>CleanupInterval</c>.</para>
    ///     <para><b>Why order by <c>OccurredAt</c> ASC.</b> The
    ///     oldest rows are deleted first so a partially-completed
    ///     sweep (cancellation / DB blip) leaves the most-recent rows
    ///     intact for the next attempt. The
    ///     <c>(org_id, occurred_at desc)</c> index added in 5.1
    ///     doesn't lead with <c>occurred_at</c>, so the planner
    ///     falls back to a full scan on <c>occurred_at &lt; cutoff</c>;
    ///     acceptable for the daily-sweep cadence + the index on
    ///     <c>(occurred_at)</c> that's part of the table's primary
    ///     btree.</para>
    ///     <para><b>Why the empty-batch early-break.</b> A single
    ///     batch returning fewer than <c>BatchSize</c> rows is the
    ///     natural end-of-sweep signal — the next call would also
    ///     return an empty batch. Stopping saves a redundant
    ///     SELECT/DELETE round-trip.</para>
    /// </remarks>
    /// <param name="db">Resolved <see cref="AuditDbContext" /> —
    /// production code passes one opened by the BackgroundService;
    /// unit tests pass an in-memory instance.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for
    /// the cutoff computation.</param>
    /// <param name="options">Bound audit options — drives the
    /// retention window and the batch size.</param>
    /// <param name="logger">Structured logger for the rowcount.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task<int> SweepAsync(
        AuditDbContext db,
        TimeProvider clock,
        AuditOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var cutoff = ComputeRetentionCutoff(clock, options);

        var deletedTotal = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await db.AuditEntries
                .Where(entry => entry.OccurredAt < cutoff)
                .OrderBy(entry => entry.OccurredAt)
                .Take(options.BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            db.AuditEntries.RemoveRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            deletedTotal += batch.Count;

            if (batch.Count < options.BatchSize)
            {
                break;
            }
        }

        if (deletedTotal > 0)
        {
            logger.LogInformation(
                "AuditRetentionService: deleted {Deleted} rows older than {Cutoff}",
                deletedTotal,
                cutoff);
        }

        return deletedTotal;
    }

    /// <summary>
    ///     Build an in-memory <see cref="AuditEntry" /> row with the
    ///     supplied anchor + offset. Used by unit tests to seed
    ///     synthetic aged rows; production code constructs
    ///     <see cref="AuditEntry" /> through the
    ///     <c>DbAuditEmitter</c> path.
    /// </summary>
    /// <param name="occurredAt"></param>
    /// <param name="orgId"></param>
    internal static AuditEntry BuildRow(DateTimeOffset occurredAt, Guid orgId)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            Action = "test.event",
            OrgId = orgId,
            ActorUserId = Guid.NewGuid(),
            TargetKind = "test",
            TargetId = Guid.NewGuid(),
            PayloadJson = "{}",
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
        };
    }
}
