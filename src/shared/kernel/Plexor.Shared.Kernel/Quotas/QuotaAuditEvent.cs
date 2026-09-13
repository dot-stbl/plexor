// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaAuditEvent — stable event-name enum for the quota subsystem's audit
// surface. v1 implementation is structured logging (LoggingQuotaAuditEmitter);
// the Phase 5+ Atlas swap replaces the logger with atlas.audit_entries
// inserts behind the same IQuotaAuditEmitter interface, so the enum + the
// wire-name mapping stay unchanged across the migration.
//
// Lives in Plexor.Shared.Kernel because both the IQuotaEnforcer
// implementation (in Plexor.Modules.Quotas.Infrastructure) and the
// QuotasController emit events — the enum sits at the kernel boundary
// the same way IQuotaEnforcer + QuotaCheckResult do.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Stable, enumerable audit-event surface for the quotas subsystem.
///     Each member maps to a single dot.case wire name via
///     <see cref="QuotaAuditEventExtensions.WireName" />; consumers (log
///     aggregators, the future atlas.audit_entries schema) filter on the
///     wire name, never on the enum value.
/// </summary>
/// <remarks>
///     <para><b>Why an enum, not magic strings.</b> Adds a member is
///     exhaustively catchable via the compiler — a new event without a
///     wire-name mapping fails the build (see
///     <c>QuotaAuditEventExtensions.WireName</c>). The wire name itself
///     remains a string so log aggregators / future SQL filters don't
///     need a C# round-trip.</para>
///     <para><b>Why in Plexor.Shared.Kernel, not in a Quotas module.</b>
///     The enforcer (Infrastructure layer) and the controller (Api layer)
///     both emit events from inside Plexor.Modules.Quotas — but the
///     interface <see cref="IQuotaAuditEmitter" /> is consumed by code
///     that doesn't need to know the Quotas module exists. Mirrors the
///     placement of <see cref="IQuotaEnforcer" />, <see cref="QuotaScope" />,
///     and <see cref="QuotaCheckResult" />.</para>
/// </remarks>
public enum QuotaAuditEvent
{
    /// <summary>
    ///     A <c>QuotaAssignment</c> was created or updated via
    ///     <c>PUT /api/v1/quotas/assignments</c>.
    /// </summary>
    AssignmentChanged = 0,

    /// <summary>
    ///     A <c>QuotaAssignment</c> was deleted via
    ///     <c>DELETE /api/v1/quotas/assignments/{id}</c>.
    /// </summary>
    AssignmentRemoved = 1,

    /// <summary>
    ///     The enforcer returned <see cref="QuotaCheckResult.Denied" /> —
    ///     the request was over the effective limit.
    /// </summary>
    UsageExceeded = 2,

    /// <summary>
    ///     The enforcer returned <see cref="QuotaCheckResult.AllowedWithWarning" />
    ///     — the request succeeded but crossed the 80% threshold.
    /// </summary>
    LimitApproaching = 3,
}

/// <summary>
///     Wire-name mapping for <see cref="QuotaAuditEvent" />. Centralised
///     so log aggregators and the future <c>atlas.audit_entries</c>
///     schema share one source of truth for the string discriminator.
/// </summary>
public static class QuotaAuditEventExtensions
{
    /// <summary>
    ///     Stable dot.case wire name of the event. Use this in log scopes,
    ///     structured-log filters, and (Phase 5+) the
    ///     <c>audit_entries.action</c> column. Changing a wire name is a
    ///     breaking change for log consumers — treat the string as part
    ///     of the public surface.
    /// </summary>
    /// <param name="auditEvent">The event to render.</param>
    /// <returns>The wire name, e.g. <c>"quotas.assignment.changed"</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="auditEvent" /> is not a known
    ///     <see cref="QuotaAuditEvent" /> value. The exhaustive switch
    ///     guarantees this only fires when a new enum member is added
    ///     without a corresponding wire-name mapping.
    /// </exception>
    public static string WireName(this QuotaAuditEvent auditEvent)
    {
        return auditEvent switch
        {
            QuotaAuditEvent.AssignmentChanged => "quotas.assignment.changed",
            QuotaAuditEvent.AssignmentRemoved => "quotas.assignment.removed",
            QuotaAuditEvent.UsageExceeded => "quotas.usage.exceeded",
            QuotaAuditEvent.LimitApproaching => "quotas.limit.approaching",
            _ => throw new ArgumentOutOfRangeException(
                nameof(auditEvent),
                auditEvent,
                "unknown QuotaAuditEvent — add a wire-name mapping for this value"),
        };
    }
}
