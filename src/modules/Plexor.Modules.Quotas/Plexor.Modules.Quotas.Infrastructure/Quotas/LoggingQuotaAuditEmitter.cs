// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoggingQuotaAuditEmitter — v1 implementation of IQuotaAuditEmitter.
// Writes one structured log line per audit event with stable dot.case
// wire names; Phase 5+ swaps this for an atlas.audit_entries insert
// behind the same interface — call sites stay unchanged.
//
// All event fields are pushed via BeginScope so the console formatter
// (and any OTLP exporter in front of it) sees every field on every
// line. The message body carries the minimum context to make a log
// line scannable; structured consumers key off the scope.
// ============================================================================

using Microsoft.Extensions.Logging;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     v1 <see cref="IQuotaAuditEmitter" />. Writes a structured log
///     line per audit event with the wire name in <c>audit_event</c>
///     scope and the rest of the context fields as scope properties.
///     Phase 5+ Atlas swap replaces this with an
///     <c>atlas.audit_entries</c> insert behind the same interface —
///     callers do not change.
/// </summary>
/// <param name="logger">Scoped <see cref="ILogger{T}" /> — the message
/// carries the wire name; the scope carries the rest of the context.
/// All events go through one logger so log routing / level overrides
/// apply uniformly.</param>
/// <remarks>
///     <para><b>Why scoped.</b> The emitter shares the per-request
///     scope with the controller / enforcer that calls it. A scoped
///     lifetime means <see cref="ILogger{TCategoryName}" /> can be
///     routed through the same DI graph as everything else — Phase 5+
///     can add request-id / correlation-id enrichment without changing
///     this signature.</para>
///     <para><b>Why a try/catch around the entire emit.</b> An audit
///     failure (logger exception, message-template bug, ...) must never
///     break the user-facing request — the whole point of the
///     <see cref="IQuotaAuditEmitter" /> abstraction is fire-and-forget
///     semantics. The catch logs at <c>LogLevel.Critical</c> and
///     swallows; the calling code completes its primary work
///     unaffected.</para>
///     <para><b>LogLevel by event.</b>
///     <list type="bullet">
///         <item><see cref="QuotaAuditEvent.UsageExceeded" />,
///         <see cref="QuotaAuditEvent.LimitApproaching" /> → <c>Warning</c>
///         (operator-actionable, not an error).</item>
///         <item><see cref="QuotaAuditEvent.AssignmentChanged" />,
///         <see cref="QuotaAuditEvent.AssignmentRemoved" /> →
///         <c>Information</c> (routine admin action).</item>
///     </list>
///     The threshold-crossing events fire at <c>Warning</c> so a noisy
///     "approaching limit" alert doesn't pollute the <c>Information</c>
///     feed.</para>
/// </remarks>
public sealed class LoggingQuotaAuditEmitter(
    ILogger<LoggingQuotaAuditEmitter> logger) : IQuotaAuditEmitter
{
    /// <inheritdoc />
    public Task EmitAsync(
        QuotaAuditEvent auditEvent,
        QuotaAuditContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var wireName = auditEvent.WireName();
            using var scope = LoggingQuotaAuditEmitterHelpers.BeginEventScope(logger, auditEvent, context);
            LoggingQuotaAuditEmitterHelpers.LogEvent(logger, auditEvent, context, wireName);
        }
        catch (Exception exception)
        {
            // Audit emission must never break a user request.
            // Swallow + log at critical so the operator sees the failure.
            logger.LogCritical(
                exception,
                "QuotaAuditEmitter: failed to emit {WireName}",
                auditEvent.WireName());
        }

        return Task.CompletedTask;
    }
}
