// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IQuotaAuditEmitter — abstraction over quota-audit emission. v1 ships a
// structured-logging implementation (LoggingQuotaAuditEmitter); Phase 5+
// swaps it for an atlas.audit_entries insert behind the same interface.
//
// Lives in Plexor.Shared.Kernel so the IQuotaEnforcer implementation (in
// Plexor.Modules.Quotas.Infrastructure) and the QuotasController (in
// Plexor.Modules.Quotas.Api) both depend on the contract without
// cross-importing the concrete emitter.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Emits a quota audit event. The v1 implementation
///     (<c>LoggingQuotaAuditEmitter</c>) writes one structured log line per
///     event with stable wire names; the Phase 5+ Atlas swap replaces it
///     with an <c>atlas.audit_entries</c> insert. Call sites stay
///     unchanged across the migration.
/// </summary>
/// <remarks>
///     <para><b>Must not throw.</b> An audit failure must never break a
///     user request — implementations wrap their work in a try/catch and
///     surface failures via a critical-level log line. The exception
///     boundary is enforced by the <c>LoggingQuotaAuditEmitter</c>'s
///     internal catch; a future DB-backed implementation owns the same
///     contract.</para>
///     <para><b>Why async on a synchronous logging implementation.</b>
///     The log path is microseconds, but the contract is <c>Task</c>
///     because the Phase 5+ DB insert will be a real I/O call. Awaiting
///     inline is acceptable today — the cost is one or two log-method
///     allocations per emit — and removes a breaking-change later.</para>
/// </remarks>
public interface IQuotaAuditEmitter
{
    /// <summary>
    ///     Emit a structured audit event with the supplied context.
    ///     Implementations MUST NOT throw; an audit failure is recorded
    ///     at <c>LogLevel.Critical</c> and swallowed.
    /// </summary>
    /// <param name="auditEvent">Which quota audit event fired — see
    /// <see cref="QuotaAuditEventExtensions.WireName" />.</param>
    /// <param name="context">Caller-supplied context (org, actor,
    /// scope, usage snapshot, ...).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task EmitAsync(
        QuotaAuditEvent auditEvent,
        QuotaAuditContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Context payload for a <see cref="IQuotaAuditEmitter.EmitAsync" />
///     call. Every field maps to a column the future
///     <c>atlas.audit_entries</c> schema will own — the field names
///     are deliberately stable so the v1 logging implementation and the
///     Phase 5+ DB-backed implementation agree on the wire shape.
/// </summary>
/// <param name="OrgId">
///     Tenant boundary — every audit entry is scoped to one org so log
///     filters + future SQL queries can slice by tenant without joining.
/// </param>
/// <param name="ActorUserId">
///     Id of the user (or API-key owner) that triggered the event. Null
///     when the event is system-driven (e.g. a background reconciliation
///     job); required for user-driven emits.
/// </param>
/// <param name="DefinitionKey">
///     Stable catalog key the event refers to
///     (<c>"compute.vms.count"</c>, <c>"api.requests.per_hour.org"</c>).
///     The DEL event uses <c>"(deleted)"</c> when the catalog row was
///     removed before the audit emit fired.
/// </param>
/// <param name="ScopeKind">
///     Wire-form scope discriminator — <c>"Org"</c>, <c>"Team"</c>,
///     <c>"Folder"</c>. Matches the enum member name
///     (<see cref="QuotaScopeKind" />.ToString()) for parity with the
///     API response surface.
/// </param>
/// <param name="ScopeId">Id of the matching Realm entity.</param>
/// <param name="Used">
///     Current usage at the time of the event. Set on <c>UsageExceeded</c>
///     and <c>LimitApproaching</c>; null otherwise.
/// </param>
/// <param name="Limit">
///     Effective limit at the time of the event. Set on
///     <c>UsageExceeded</c> and <c>LimitApproaching</c>; null otherwise.
/// </param>
/// <param name="Requested">
///     Amount the caller tried to reserve. Set on <c>UsageExceeded</c> and
///     <c>LimitApproaching</c>; null otherwise.
/// </param>
/// <param name="ThresholdPct">
///     The 80 (or future 50/95) threshold that fired. Set on
///     <c>LimitApproaching</c>; null otherwise.
/// </param>
/// <param name="AssignmentId">
///     Id of the affected <c>QuotaAssignment</c> row. Set on
///     <c>AssignmentChanged</c> and <c>AssignmentRemoved</c>; null
///     otherwise.
/// </param>
public sealed record QuotaAuditContext(
    Guid OrgId,
    Guid? ActorUserId,
    string DefinitionKey,
    string ScopeKind,
    Guid ScopeId,
    decimal? Used = null,
    decimal? Limit = null,
    decimal? Requested = null,
    decimal? ThresholdPct = null,
    Guid? AssignmentId = null);
