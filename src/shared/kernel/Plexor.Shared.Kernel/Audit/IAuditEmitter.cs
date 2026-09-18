// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IAuditEmitter — generic kernel contract for emitting audit events.
// Implementations write to durable storage (Postgres via DbAuditEmitter
// in 5.1, log/queue/etc. as future needs arise). MUST NOT throw — audit
// emission failure must never break a user request.
//
// Lives in Plexor.Shared.Kernel because every module that emits audit
// events (Quotas via the IQuotaAuditEmitter delegation, Realm via
// OrgAuthProvidersController in 5.2, future modules) depends on the
// contract — not on Plexor.Modules.Audit. Same placement discipline as
// IQuotaAuditEmitter (a quota-specific convenience over the generic
// kernel contract).
// ============================================================================

namespace Plexor.Shared.Kernel.Audit;

/// <summary>
///     Generic audit-event emission surface. Replaces the per-module
///     ad-hoc logging patterns that v1 used (e.g.
///     <c>LoggingQuotaAuditEmitter</c>) with one cross-cutting contract
///     so every event flows through the same store and the same
///     wire-format guarantees.
/// </summary>
/// <remarks>
///     <para><b>Must not throw.</b> An audit failure must never break a
///     user request — the abstraction exists precisely so audit emission
///     is fire-and-forget. Implementations wrap their work in a
///     try/catch and surface failures via a critical-level log line.</para>
///     <para><b>Async on a synchronous implementation.</b> Even when the
///     backing store is fast (a single INSERT round-trip), the contract
///     returns <see cref="Task" /> because (a) the DB-backed emitter is
///     a real I/O call and (b) a future in-process channel consumer
///     needs the <see cref="Task" /> shape to enqueue. Awing inline is
///     acceptable today — the cost is one extra allocation per emit —
///     and removes a breaking-change later.</para>
///     <para><b>Stable wire names.</b> The <c>action</c> parameter
///     is one of the <see cref="AuditActions" /> constants — never a
///     free-form string. New modules add a new constant there before
///     they emit, so the admin UI's event-type filter stays
///     exhaustive.</para>
/// </remarks>
public interface IAuditEmitter
{
    /// <summary>
    ///     Emit one audit event. Writes one row to
    ///     <c>atlas.audit_entries</c> with the wire name, tenant
    ///     scope, optional actor, target pointer, and JSON-encoded
    ///     payload.
    /// </summary>
    /// <param name="action">
    ///     Stable dot.case wire name from
    ///     <see cref="AuditActions" />. Never an ad-hoc string.
    /// </param>
    /// <param name="context">
    ///     Tenant scope + optional actor + target pointer + action-
    ///     specific payload dict.
    /// </param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    ///     A completed <see cref="Task" />. Implementations that hit
    ///     an internal error log + swallow; the returned task always
    ///     completes successfully.
    /// </returns>
    public Task EmitAsync(
        string action,
        AuditContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Payload for an <see cref="IAuditEmitter.EmitAsync" /> call.
///     Fields are stable so the wire-format on the
///     <c>audit_entries.payload_json</c> column is parseable by
///     future consumers (admin UI 5.3, log aggregators).
/// </summary>
/// <param name="OrgId">
///     Tenant boundary. Always populated (audit emission rejects
///     events without a tenant scope). Denormalized into the row
///     for tenant-scoped admin queries without joining.
/// </param>
/// <param name="ActorUserId">
///     Id of the user (or API-key owner) that triggered the event.
///     Null when the event is system-driven (migrator, retention
///     sweeper). For human-driven emits this is the caller's
///     <c>ICurrentUser.UserId</c>.
/// </param>
/// <param name="TargetKind">
///     Short discriminator of what the event refers to
///     (<c>"quota_assignment"</c>, <c>"quota_usage"</c>,
///     <c>"org_auth_provider_config"</c>). Bounded-cardinality
///     string suitable for a SQL index + admin filter dropdown.
/// </param>
/// <param name="TargetId">
///     Id of the affected entity. Null when the event doesn't
///     reference a single row (rare — most events have a target).
/// </param>
/// <param name="Payload">
///     Action-specific context keys. The keys are documented on each
///     <see cref="AuditActions" /> constant. Serialized to JSON via
///     <see cref="System.Text.Json.JsonSerializerOptions.Web" /> and
///     stored on the <c>audit_entries.payload_json</c> column as
///     <c>jsonb</c>.
/// </param>
public sealed record AuditContext(
    Guid OrgId,
    Guid? ActorUserId,
    string TargetKind,
    Guid? TargetId,
    IReadOnlyDictionary<string, object?> Payload);
