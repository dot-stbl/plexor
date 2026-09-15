// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditOptions — IOptions-bound configuration for the audit module.
// Owned by the Audit Application layer (the only layer that surfaces
// retention defaults to the host) and bound from the [Audit] section
// of plexor.yaml / PLX_AUDIT_* env vars by the composition root
// (Plexor.Host Program.cs + Plexor.Migrator Program.cs).
//
// Validation: Range attributes are enforced by ValidateDataAnnotations()
// chained with ValidateOnStart() in the composition root — a bad value
// (RetentionDays < 1, BatchSize out of range, etc.) fails the host
// startup, not the first sweep.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace Plexor.Modules.Audit.Application.Audit;

/// <summary>
///     Runtime configuration for the audit capability. v1 ships
///     three knobs:
///     <list type="bullet">
///         <item>
///             <see cref="RetentionDays" /> — how long audit rows
///             stick around before the daily sweep deletes them.
///             Default 90 days; 1 day to 10 years is the supported
///             span.
///         </item>
///         <item>
///             <see cref="CleanupInterval" /> — how often the sweep
///             loop wakes up. Default 24h; the next-fire anchor is
///             <see cref="SweepHourUtc" /> on the very first wake.
///         </item>
///         <item>
///             <see cref="BatchSize" /> — DELETE rows per batch.
///             Bounds the longest transaction the sweep takes.
///             Default 10_000.
///         </item>
///         <item>
///             <see cref="SweepHourUtc" /> — hour-of-day the sweep
///             fires on first wake (0-23). Default 5 (05:00 UTC) —
///             the operator-quiet window in the typical Plexor
///             deployment.
///         </item>
///     </list>
/// </summary>
/// <remarks>
///     <para><b>Per-action / per-org retention</b> is out of scope
///     for v1 — global retention is the contract. The shape leaves
///     room for a future <c>PerActionRetention</c> dictionary without
///     breaking the v1 binding.</para>
/// </remarks>
public sealed class AuditOptions
{
    /// <summary>
    ///     Config section name; matches <c>plexor.yaml</c> →
    ///     <c>[Audit]</c> or <c>PLX_AUDIT_*</c> env vars.
    /// </summary>
    public const string SectionName = "Audit";

    /// <summary>
    ///     Audit rows older than this are deleted by the daily
    ///     sweep. Default 90 days. Lower bound 1 (no zero-day
    ///     retention — disable the sweep via the host config instead);
    ///     upper bound 3650 (10 years — a deliberate cap to keep the
    ///     retention window from accidentally capturing forever).
    /// </summary>
    [Range(1, 3650)]
    public int RetentionDays { get; init; } = 90;

    /// <summary>
    ///     How often the retention sweep runs. Default 24h; the
    ///     very first sweep fires at <see cref="SweepHourUtc" />
    ///     on the next UTC day, and every
    ///     <see cref="CleanupInterval" /> after that.
    /// </summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Rows deleted per batch — bounds the longest transaction
    ///     the sweep takes. Default 10_000. Lower bound 100
    ///     (smaller batches don't help — the per-batch overhead
    ///     dominates); upper bound 100_000 (one DELETE larger than
    ///     this risks lock escalation on Postgres).
    /// </summary>
    [Range(100, 100_000)]
    public int BatchSize { get; init; } = 10_000;

    /// <summary>
    ///     Hour-of-day (UTC) the sweep fires on first wake. Default
    ///     5 (05:00 UTC) — the operator-quiet window. Range 0-23.
    /// </summary>
    [Range(0, 23)]
    public int SweepHourUtc { get; init; } = 5;
}
