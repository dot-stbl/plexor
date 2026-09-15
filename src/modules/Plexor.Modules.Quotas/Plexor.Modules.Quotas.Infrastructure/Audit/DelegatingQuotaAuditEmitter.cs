// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DelegatingQuotaAuditEmitter — IQuotaAuditEmitter implementation that
// adapts the quota-specific QuotaAuditContext to the generic
// IAuditEmitter contract. Replaces the v1 LoggingQuotaAuditEmitter at
// the DI seam; the three call sites (EfQuotaEnforcer +
// QuotasController.UpsertAssignment + QuotasController.DeleteAssignment)
// stay unchanged.
//
// The mapper is intentionally thin — its only job is to translate the
// quota-shaped context into the audit-shaped context (wire name +
// target_kind + payload dict). The actual storage work lives in
// DbAuditEmitter so a future swap to a different backend (queue, log
// aggregator, ...) doesn't require a new quota-specific adapter.
// ============================================================================

using Plexor.Shared.Kernel.Audit;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Audit;

/// <summary>
///     Quotas-side adapter that turns <see cref="QuotaAuditEvent" /> +
///     <see cref="QuotaAuditContext" /> into an
///     <see cref="IAuditEmitter.EmitAsync" /> call. Backed by the
///     generic <c>DbAuditEmitter</c> (in <c>Plexor.Modules.Audit.Infrastructure</c>)
///     via DI; the call site does not know which backend the kernel
///     contract writes to.
/// </summary>
/// <param name="inner">
///     Resolved <see cref="IAuditEmitter" /> — in 5.1 this is the
///     EF-backed <c>DbAuditEmitter</c>; future phases may swap it for
///     a channel-backed or log-only emitter without touching this
///     adapter.
/// </param>
/// <remarks>
///     <para><b>TargetKind mapping.</b> The four quota events split
///     into two target kinds: assignment CRUD events
///     (<c>AssignmentChanged</c>, <c>AssignmentRemoved</c>) point at
///     the affected <c>quota_assignment</c> row; the enforcer events
///     (<c>UsageExceeded</c>, <c>LimitApproaching</c>) describe a
///     usage snapshot rather than a specific row, so
///     <c>TargetKind = "quota_usage"</c> with <c>TargetId = null</c>.
///     Future enum additions fall through to a safe
///     <c>"quota"</c> default so the emitter never throws on an
///     unknown event.</para>
///     <para><b>Payload key set.</b> The payload dict carries every
///     field that <see cref="QuotaAuditContext" /> exposes — the
///     generic <see cref="IAuditEmitter" /> doesn't know which keys
///     the quota events need, so this adapter forwards them all.
///     Consumers (admin UI 5.3, log aggregators) read keys
///     documented on the <see cref="AuditActions" /> constants.</para>
///     <para><b>No try/catch here.</b> The IQuotaAuditEmitter
///     contract says "must not throw"; the inner IAuditEmitter
///     implementation already enforces the same rule (DbAuditEmitter
///     swallows DB / serialization errors). Two swallows would be
///     wasteful — this adapter trusts the inner to keep its promise.</para>
/// </remarks>
public sealed class DelegatingQuotaAuditEmitter(IAuditEmitter inner) : IQuotaAuditEmitter
{
    /// <inheritdoc />
    public async Task EmitAsync(
        QuotaAuditEvent auditEvent,
        QuotaAuditContext context,
        CancellationToken cancellationToken)
    {
        var action = auditEvent.WireName();

        var targetKind = auditEvent switch
        {
            QuotaAuditEvent.AssignmentChanged or QuotaAuditEvent.AssignmentRemoved => "quota_assignment",
            QuotaAuditEvent.UsageExceeded or QuotaAuditEvent.LimitApproaching => "quota_usage",
            _ => "quota",
        };

        // All ten QuotaAuditContext fields are forwarded as payload
        // keys — null fields serialize as JSON null, which consumers
        // can distinguish from "key absent". The future admin UI
        // (5.3) will only display non-null fields, but the row
        // shape stays stable so SQL queries against payload_json can
        // match on key presence without breaking when a field is
        // populated for one event and not another.
        var payload = new Dictionary<string, object?>
        {
            ["definition_key"] = context.DefinitionKey,
            ["scope_kind"] = context.ScopeKind,
            ["scope_id"] = context.ScopeId,
            ["used"] = context.Used,
            ["limit"] = context.Limit,
            ["requested"] = context.Requested,
            ["threshold_pct"] = context.ThresholdPct,
            ["assignment_id"] = context.AssignmentId,
        };

        // For usage events TargetId stays null — there's no single
        // row to point at. For assignment events TargetId is the
        // QuotaAssignment.Id the controller / enforcer touched.
        var targetId = targetKind == "quota_assignment"
            ? context.AssignmentId
            : null;

        await inner.EmitAsync(
            action,
            new AuditContext(
                OrgId: context.OrgId,
                ActorUserId: context.ActorUserId,
                TargetKind: targetKind,
                TargetId: targetId,
                Payload: payload),
            cancellationToken);
    }
}
