// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DbAuditEmitterShould — exercise the IAuditEmitter implementation in
// isolation. The five tests here pin:
//
//   1. EmitAsync writes a row with the supplied action + org + target.
//   2. PayloadJson is the JSON-serialized payload dict.
//   3. ActorUserId propagates from the context.
//   4. Null ActorUserId stays null in the row.
//   5. A failure on SaveChangesAsync is caught + swallowed —
//      EmitAsync completes successfully even when the DbContext throws
//      (the IAuditEmitter contract is fire-and-forget).
//
// Uses the in-memory AuditDbContext for happy-path tests; the
// "must not throw" test substitutes a logger and breaks the context
// with a stub that throws on SaveChangesAsync.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.Modules.Audit.Domain.Entities;
using Plexor.Modules.Audit.Infrastructure.Audit;
using Plexor.Modules.Audit.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Audit;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Audit.Unit.Audit;

/// <summary>
///     Behavioural tests for <see cref="DbAuditEmitter" />. The
///     emitter writes one row per call to <c>atlas.audit_entries</c>;
///     the tests assert the column mapping, the payload JSON shape,
///     the no-throw contract, and the null-actor allow path.
/// </summary>
public sealed class DbAuditEmitterShould
{
    /// <summary>
    ///     Given a context with a populated action + target, when
    ///     <see cref="DbAuditEmitter.EmitAsync" /> runs, then one row
    ///     is inserted into <c>audit_entries</c> with the supplied
    ///     fields + the clock's UtcNow stamp on
    ///     <c>occurred_at</c> + <c>created_at</c>.
    /// </summary>
    [Fact(DisplayName = "Given action + context, when EmitAsync runs, then writes a row with the supplied fields")]
    public async Task EmitAsync_WritesRow_WithCorrectFieldsAsync()
    {
        var db = await AuditTestDb.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
        var sut = new DbAuditEmitter(db, clock, NullLogger<DbAuditEmitter>.Instance);
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        await sut.EmitAsync(
            AuditActions.QuotasAssignmentChanged,
            new AuditContext(
                OrgId: orgId,
                ActorUserId: actorId,
                TargetKind: "quota_assignment",
                TargetId: targetId,
                Payload: new Dictionary<string, object?>
                {
                    ["definition_key"] = "compute.vms.count",
                }),
            CancellationToken.None);

        var row = await db.AuditEntries.SingleAsync();
        row.Action.ShouldBe(AuditActions.QuotasAssignmentChanged);
        row.OrgId.ShouldBe(orgId);
        row.ActorUserId.ShouldBe(actorId);
        row.TargetKind.ShouldBe("quota_assignment");
        row.TargetId.ShouldBe(targetId);
        row.OccurredAt.ShouldBe(clock.GetUtcNow());
        row.CreatedAt.ShouldBe(clock.GetUtcNow());
    }

    /// <summary>
    ///     Given a context whose payload dict has multiple keys, when
    ///     <see cref="DbAuditEmitter.EmitAsync" /> runs, then the
    ///     row's <c>payload_json</c> column carries every key as JSON.
    /// </summary>
    [Fact(DisplayName = "Given multi-key payload, when EmitAsync runs, then payload_json contains every key")]
    public async Task EmitAsync_PopulatesPayloadJson_FromPayloadDictAsync()
    {
        var db = await AuditTestDb.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sut = new DbAuditEmitter(db, clock, NullLogger<DbAuditEmitter>.Instance);

        await sut.EmitAsync(
            AuditActions.QuotasUsageExceeded,
            new AuditContext(
                OrgId: Guid.NewGuid(),
                ActorUserId: Guid.NewGuid(),
                TargetKind: "quota_usage",
                TargetId: null,
                Payload: new Dictionary<string, object?>
                {
                    ["definition_key"] = "compute.vms.count",
                    ["scope_kind"] = "Org",
                    ["scope_id"] = Guid.NewGuid(),
                    ["used"] = 100m,
                    ["limit"] = 100m,
                    ["requested"] = 1m,
                }),
            CancellationToken.None);

        var row = await db.AuditEntries.SingleAsync();
        row.PayloadJson.ShouldContain("\"definition_key\"");
        row.PayloadJson.ShouldContain("compute.vms.count");
        row.PayloadJson.ShouldContain("\"used\":100");
        row.PayloadJson.ShouldContain("\"limit\":100");
        row.PayloadJson.ShouldContain("\"requested\":1");
    }

    /// <summary>
    ///     Given a context with <c>ActorUserId = null</c> (system-driven
    ///     emit), when <see cref="DbAuditEmitter.EmitAsync" /> runs,
    ///     then the inserted row carries a null actor_user_id column.
    ///     Pins the nullable-actor allow path used by background
    ///     sweepers / migrator-seeded audit events.
    /// </summary>
    [Fact(DisplayName = "Given ActorUserId = null, when EmitAsync runs, then row allows null actor_user_id")]
    public async Task EmitAsync_WithNullActorUserId_AllowsNullAsync()
    {
        var db = await AuditTestDb.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sut = new DbAuditEmitter(db, clock, NullLogger<DbAuditEmitter>.Instance);

        await sut.EmitAsync(
            AuditActions.QuotasAssignmentChanged,
            new AuditContext(
                OrgId: Guid.NewGuid(),
                ActorUserId: null,
                TargetKind: "quota_assignment",
                TargetId: Guid.NewGuid(),
                Payload: new Dictionary<string, object?>()),
            CancellationToken.None);

        var row = await db.AuditEntries.SingleAsync();
        row.ActorUserId.ShouldBeNull();
    }

    /// <summary>
    ///     Given the OrgId on the context, when
    ///     <see cref="DbAuditEmitter.EmitAsync" /> runs, then the row's
    ///     org_id column matches the context's OrgId exactly. Pinned
    ///     because the audit admin endpoint (5.2) filters by org_id —
    ///     a cross-tenant leak would be a security incident.
    /// </summary>
    [Fact(DisplayName = "Given OrgId, when EmitAsync runs, then row stores the same org_id")]
    public async Task EmitAsync_PopulatesOrgId_FromContextAsync()
    {
        var db = await AuditTestDb.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sut = new DbAuditEmitter(db, clock, NullLogger<DbAuditEmitter>.Instance);
        var orgId = Guid.NewGuid();

        await sut.EmitAsync(
            AuditActions.QuotasAssignmentChanged,
            new AuditContext(
                OrgId: orgId,
                ActorUserId: Guid.NewGuid(),
                TargetKind: "quota_assignment",
                TargetId: Guid.NewGuid(),
                Payload: new Dictionary<string, object?>()),
            CancellationToken.None);

        var row = await db.AuditEntries.SingleAsync();
        row.OrgId.ShouldBe(orgId);
    }

    /// <summary>
    ///     Given a SaveChangesAsync that throws, when
    ///     <see cref="DbAuditEmitter.EmitAsync" /> runs, then the call
    ///     completes successfully (no exception escapes) and the
    ///     failure is logged at <see cref="LogLevel.Critical" />. The
    ///     audit contract is fire-and-forget; an emitter failure must
    ///     never break a user request.
    /// </summary>
    [Fact(DisplayName = "Given a SaveChangesAsync that throws, when EmitAsync runs, then does not throw")]
    public async Task EmitAsync_OnException_DoesNotThrowAsync()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var logger = Substitute.For<ILogger<DbAuditEmitter>>();

        // Subclass of AuditDbContext that always throws on save —
        // exercises the swallow path without a real Postgres
        // instance. AuditDbContext is documented as non-sealed for
        // this exact test extension point.
        var brokenDb = new ThrowingAuditDbContext();
        var sut = new DbAuditEmitter(brokenDb, clock, logger);

        // Should not throw — audit emission failure is swallowed.
        await sut.EmitAsync(
            AuditActions.QuotasUsageExceeded,
            new AuditContext(
                OrgId: Guid.NewGuid(),
                ActorUserId: Guid.NewGuid(),
                TargetKind: "quota_usage",
                TargetId: null,
                Payload: new Dictionary<string, object?>()),
            CancellationToken.None);

        // The Critical-level swallow log fires.
        logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }

    /// <summary>
    ///     Test-only subclass that overrides the
    ///     <c>SaveChangesAsync(CancellationToken)</c> override so the
    ///     "must not throw" test can exercise the swallow path.
    ///     Lives in the test project; production code never
    ///     subclasses <see cref="AuditDbContext" />.
    /// </summary>
    private sealed class ThrowingAuditDbContext : AuditDbContext
    {
        public ThrowingAuditDbContext()
            : base(new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase($"audit-throwing-{Guid.NewGuid():N}")
                .Options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("synthetic DbContext failure");
        }
    }
}
