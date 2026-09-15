// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DelegatingQuotaAuditEmitterShould — exercise the Phase 5.1 quota-
// specific adapter that translates QuotaAuditContext into the generic
// IAuditEmitter contract. The three tests here pin:
//
//   1. AssignmentChanged wire name + target_kind = "quota_assignment".
//   2. UsageExceeded wire name + target_kind = "quota_usage" + no target_id.
//   3. ActorUserId propagates from the quota context into the
//      AuditContext the inner emitter receives.
//
// Uses NSubstitute on IAuditEmitter to assert the exact wire names,
// target_kinds, and payload dict keys the adapter forwards — the
// DbAuditEmitter side is exercised by DbAuditEmitterShould; this file
// focuses on the adapter's mapping contract.
// ============================================================================

using NSubstitute;
using Plexor.Modules.Quotas.Infrastructure.Audit;
using Plexor.Shared.Kernel.Audit;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Quotas.Unit.Audit;

/// <summary>
///     Behavioural tests for <see cref="DelegatingQuotaAuditEmitter" />.
///     Asserts the adapter's mapping from
///     <see cref="QuotaAuditEvent" /> + <see cref="QuotaAuditContext" />
///     to the generic <see cref="IAuditEmitter.EmitAsync" /> call:
///     wire name comes from the event enum, target_kind depends on
///     the event family, and every quota context field forwards as a
///     payload key.
/// </summary>
public sealed class DelegatingQuotaAuditEmitterShould
{
    /// <summary>
    ///     Given <see cref="QuotaAuditEvent.AssignmentChanged" />, when
    ///     <see cref="DelegatingQuotaAuditEmitter.EmitAsync" /> runs,
    ///     then the inner <see cref="IAuditEmitter" /> is called with
    ///     the wire name <c>"quotas.assignment.changed"</c>,
    ///     <c>target_kind = "quota_assignment"</c>, and the
    ///     assignment id as the target id.
    /// </summary>
    [Fact(DisplayName = "Given AssignmentChanged, when EmitAsync runs, then inner receives quota_assignment target_kind + correct wire name")]
    public async Task EmitAsync_WithAssignmentChanged_DelegatesWithCorrectWireNameAsync()
    {
        var inner = Substitute.For<IAuditEmitter>();
        var sut = new DelegatingQuotaAuditEmitter(inner);
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var context = SampleContext(
            orgId: orgId,
            actorId: actorId,
            scopeId: scopeId,
            assignmentId: assignmentId,
            definitionKey: "compute.vms.count");

        await sut.EmitAsync(QuotaAuditEvent.AssignmentChanged, context, CancellationToken.None);

        await inner.Received(1).EmitAsync(
            AuditActions.QuotasAssignmentChanged,
            Arg.Is<AuditContext>(c =>
                c.OrgId == orgId &&
                c.ActorUserId == actorId &&
                c.TargetKind == "quota_assignment" &&
                c.TargetId == assignmentId &&
                c.Payload["definition_key"] as string == "compute.vms.count" &&
                c.Payload["scope_kind"] as string == "Org" &&
                c.Payload["scope_id"] as Guid? == scopeId &&
                c.Payload["assignment_id"] as Guid? == assignmentId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Given <see cref="QuotaAuditEvent.UsageExceeded" />, when
    ///     <see cref="DelegatingQuotaAuditEmitter.EmitAsync" /> runs,
    ///     then the inner <see cref="IAuditEmitter" /> is called with
    ///     <c>target_kind = "quota_usage"</c> and a null target id
    ///     (the enforcer points at a snapshot, not a single row).
    ///     The numeric payload fields (used, limit, requested)
    ///     propagate through to the audit context.
    /// </summary>
    [Fact(DisplayName = "Given UsageExceeded, when EmitAsync runs, then inner receives quota_usage target_kind + numeric payload")]
    public async Task EmitAsync_WithUsageExceeded_MapsToQuotaUsageTargetAsync()
    {
        var inner = Substitute.For<IAuditEmitter>();
        var sut = new DelegatingQuotaAuditEmitter(inner);
        var orgId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var context = SampleContext(
            orgId: orgId,
            scopeId: scopeId,
            used: 100m,
            limit: 100m,
            requested: 1m,
            definitionKey: "compute.vms.count");

        await sut.EmitAsync(QuotaAuditEvent.UsageExceeded, context, CancellationToken.None);

        await inner.Received(1).EmitAsync(
            AuditActions.QuotasUsageExceeded,
            Arg.Is<AuditContext>(c =>
                c.OrgId == orgId &&
                c.TargetKind == "quota_usage" &&
                c.TargetId == null &&
                c.Payload["used"] as decimal? == 100m &&
                c.Payload["limit"] as decimal? == 100m &&
                c.Payload["requested"] as decimal? == 1m),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Given a quota context whose <see cref="QuotaAuditContext.ActorUserId" />
    ///     is null (system-driven emit), when
    ///     <see cref="DelegatingQuotaAuditEmitter.EmitAsync" /> runs,
    ///     then the inner <see cref="IAuditEmitter" /> receives an
    ///     <see cref="AuditContext" /> with the same null actor.
    ///     Pins the actor passthrough path.
    /// </summary>
    [Fact(DisplayName = "Given ActorUserId = null, when EmitAsync runs, then inner receives null ActorUserId")]
    public async Task EmitAsync_PassesActorUserIdThroughAsync()
    {
        var inner = Substitute.For<IAuditEmitter>();
        var sut = new DelegatingQuotaAuditEmitter(inner);
        var context = SampleContext(actorId: null, assignmentId: Guid.NewGuid());

        await sut.EmitAsync(QuotaAuditEvent.AssignmentRemoved, context, CancellationToken.None);

        await inner.Received(1).EmitAsync(
            AuditActions.QuotasAssignmentRemoved,
            Arg.Is<AuditContext>(c => c.ActorUserId == null && c.TargetKind == "quota_assignment"),
            Arg.Any<CancellationToken>());
    }

    private static QuotaAuditContext SampleContext(
        Guid? orgId = null,
        Guid? actorId = null,
        Guid? scopeId = null,
        Guid? assignmentId = null,
        decimal? used = null,
        decimal? limit = null,
        decimal? requested = null,
        decimal? thresholdPct = null,
        string definitionKey = "compute.vms.count")
    {
        return new QuotaAuditContext(
            OrgId: orgId ?? Guid.NewGuid(),
            // Pass actorId through verbatim — a non-null Guid? is
            // set; a null Guid? stays null. ?? Guid.NewGuid() would
            // replace null with a fresh id, hiding the actor-passthrough
            // contract the third test asserts on.
            ActorUserId: actorId,
            DefinitionKey: definitionKey,
            ScopeKind: "Org",
            ScopeId: scopeId ?? Guid.NewGuid(),
            Used: used,
            Limit: limit,
            Requested: requested,
            ThresholdPct: thresholdPct,
            AssignmentId: assignmentId);
    }
}
