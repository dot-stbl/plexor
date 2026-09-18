// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DbAuditEmitter — IAuditEmitter implementation that writes one row to
// atlas.audit_entries per EmitAsync call. Replaces the v1 logging
// implementation behind the same kernel contract; the four quota audit
// events + future auth-provider events now flow through one durable
// pipeline.
//
// Why a real INSERT round-trip per emit:
//   * The cost is one indexed PK write + one jsonb column write. On
//     a current Postgres that's ~150µs p50 — well below the request
//     budget for a quota enforcement path. If the per-event cost
//     ever becomes a problem, swap the body to enqueue to a
//     Channel<T> + drain from a BackgroundService (the interface
//     stays the same, only the implementation changes).
//   * Synchronous in-line emission keeps the audit row's occurred_at
//     timestamp truthful relative to the action's commit. An
//     out-of-band drainer introduces a delay between the action and
//     the row that an admin UI timeline would render as a gap.
//
// Must NOT throw — the IAuditEmitter contract is fire-and-forget.
// All DB / serialization errors are caught and surfaced via a
// critical-level log line; the caller completes its primary work
// unaffected.
// ============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Audit.Domain.Entities;
using Plexor.Modules.Audit.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Audit;

namespace Plexor.Modules.Audit.Infrastructure.Audit;

/// <summary>
///     Scoped <see cref="IAuditEmitter" /> that writes one row to
///     <c>atlas.audit_entries</c> per call. Shares the per-request
///     <see cref="AuditDbContext" /> with the calling controller /
///     enforcer so the audit INSERT lives in the same transaction
///     boundary as the action that triggered it (the caller's
///     transaction; the audit INSERT itself is a side-effect of the
///     DbContext's <c>SaveChangesAsync</c> batch, not a separate
///     commit).
/// </summary>
/// <param name="db">Scoped <see cref="IAuditDbContext" /> —
/// production code resolves the concrete <see cref="AuditDbContext" />;
/// the narrow interface keeps the emitter unit-testable via NSubstitute
/// without unsealing the DbContext.</param>
/// <param name="clock">
///     Injected <see cref="TimeProvider" /> for the
///     <c>occurred_at</c> stamp — keeps the timestamp unit-testable
///     via a fake clock (e.g. <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>
///     in the unit-test project).
/// </param>
/// <param name="logger">
///     Critical-level sink for emit failures. The caller never sees
///     the exception.
/// </param>
/// <remarks>
///     <para><b>Atomic with the caller (when the caller is in a
///     transaction).</b> Because the emitter uses the same
///     <see cref="AuditDbContext" /> as the caller, the audit INSERT rides
///     on the caller's ambient transaction. A successful request
///     commits the audit row; a rolled-back request leaves no trace.
///     Same contract as the v1 logging implementation (the log line
///     only existed when the request committed).</para>
///     <para><b>Scoped lifetime.</b> Mirrors the per-request shape
///     of the controllers + enforcers that call it. Singleton would
///     be wrong — a DbContext captured by a singleton would never
///     be disposed.</para>
///     <para><b>JSON serialization.</b>
///     <see cref="JsonSerializerOptions.Web" /> is the framework
///     frozen/cached shared instance — camelCase property names,
///     case-insensitive reads, AllowReadingFromString. Project
///     convention bans inline <c>new JsonSerializerOptions(...)</c>
///     at call sites (<c>anti-patterns.md</c> §6).</para>
/// </remarks>
public sealed class DbAuditEmitter(
    IAuditDbContext db,
    TimeProvider clock,
    ILogger<DbAuditEmitter> logger) : IAuditEmitter
{
    /// <inheritdoc />
    public async Task EmitAsync(
        string action,
        AuditContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // VSTHRD103 false-positive — JsonSerializer.Serialize is
            // pure CPU-bound (no I/O on a small dictionary); the async
            // overload (SerializeAsync) requires a stream/pipeWriter
            // and is strictly heavier for a small payload. Suppress
            // with explanation rather than route through SerializeAsync.
            // Same escape hatch as WorkloadCommandHandlers.cs.
#pragma warning disable VSTHRD103 // Sync serialize — no I/O on a small payload dict.
            var payloadJson = JsonSerializer.Serialize(
                context.Payload,
                JsonSerializerOptions.Web);
#pragma warning restore VSTHRD103

            await db.AuditEntries.AddAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Action = action,
                OrgId = context.OrgId,
                ActorUserId = context.ActorUserId,
                TargetKind = context.TargetKind,
                TargetId = context.TargetId,
                PayloadJson = payloadJson,
                OccurredAt = clock.GetUtcNow(),
                CreatedAt = clock.GetUtcNow(),
            }, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Audit emission failure must never break a user request.
            // Log + swallow — the calling controller / enforcer
            // completes its primary work unaffected.
            logger.LogCritical(
                exception,
                "DbAuditEmitter: failed to emit {Action}",
                action);
        }
    }
}
