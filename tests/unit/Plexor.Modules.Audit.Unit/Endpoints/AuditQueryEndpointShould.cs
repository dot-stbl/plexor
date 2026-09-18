// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditQueryEndpointShould — exercise the GET /api/v1/audit handler
// in isolation. The endpoint resolves AuditDbContext (scoped, in-
// memory for unit tests via AuditTestDb) + ICurrentUser (NSubstitute).
// The five tests pin the LINQ composition that AuditQueryFilters.ApplyAsync
// owns:
//   1. Tenant scope: rows from another org never surface.
//   2. Action filter: matches only rows with the requested action.
//   3. Since filter: excludes rows older than the boundary.
//   4. Limit: clamps the page size to the requested cap.
//   5. Unknown action: returns an empty page (no exception).
//
// The ParsePayload defensive-describe path is exercised separately
// by AuditQueryParsePayloadShould — the projections helper depends
// on it but the happy path is what callers care about.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Plexor.Modules.Audit.Api.Endpoints;
using Plexor.Modules.Audit.Domain.Entities;
using Plexor.Modules.Audit.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Audit;
using Plexor.Shared.Kernel.Identity;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Audit.Unit.Endpoints;

/// <summary>
///     Behavioural tests for
///     <see cref="AuditQueryFilters.ApplyAsync" /> + the
///     <see cref="AuditQueryEndpoint.HandleAsync" /> orchestration.
///     The handler delegates query composition to
///     <c>AuditQueryFilters</c>; the tests exercise that helper
///     directly with a seeded in-memory <see cref="AuditDbContext" />
///     so the LINQ tree is asserted without standing up a real
///     Postgres instance.
/// </summary>
public sealed class AuditQueryEndpointShould
{
    /// <summary>Build a populated in-memory audit DB for the
    /// supplied <paramref name="tenantIds" />. Each tenant gets a
    /// few <c>quotas.assignment.changed</c> rows + one
    /// <c>org.auth_provider.changed</c> row at varying
    /// <c>occurred_at</c> offsets.</summary>
    private static async Task<AuditDbContext> SeedAsync(params Guid[] tenantIds)
    {
        var db = await AuditTestDb.CreateAsync();
        var baseTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        foreach (var tenantId in tenantIds)
        {
            await db.AuditEntries.AddRangeAsync(
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Action = AuditActions.QuotasAssignmentChanged,
                    OrgId = tenantId,
                    ActorUserId = Guid.NewGuid(),
                    TargetKind = "quota_assignment",
                    TargetId = Guid.NewGuid(),
                    PayloadJson = "{\"definition_key\":\"compute.vms.count\"}",
                    OccurredAt = baseTime.AddMinutes(-30),
                    CreatedAt = baseTime.AddMinutes(-30),
                },
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Action = AuditActions.QuotasAssignmentChanged,
                    OrgId = tenantId,
                    ActorUserId = Guid.NewGuid(),
                    TargetKind = "quota_assignment",
                    TargetId = Guid.NewGuid(),
                    PayloadJson = "{\"definition_key\":\"compute.clusters.count\"}",
                    OccurredAt = baseTime.AddMinutes(-15),
                    CreatedAt = baseTime.AddMinutes(-15),
                },
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Action = AuditActions.OrgAuthProviderChanged,
                    OrgId = tenantId,
                    ActorUserId = Guid.NewGuid(),
                    TargetKind = "org_auth_provider_config",
                    TargetId = tenantId,
                    PayloadJson = "{\"old_provider\":\"Sigil\",\"new_provider\":\"Oidc\"}",
                    OccurredAt = baseTime.AddMinutes(-5),
                    CreatedAt = baseTime.AddMinutes(-5),
                });
        }

        await db.SaveChangesAsync();
        return db;
    }

    private static ICurrentUser StubCurrentUser(Guid tenantId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.TenantId.Returns(tenantId);
        return currentUser;
    }

    /// <summary>Given rows for two tenants, when the query runs for
    /// tenant A, then tenant B's rows are excluded — the OrgId filter
    /// is a hard tenant boundary.</summary>
    [Fact(DisplayName = "Given rows for two tenants, when query runs for tenant A, then tenant B's rows are excluded")]
    public async Task Query_ForTenant_ReturnsOnlyTenantRowsAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = await SeedAsync(tenantA, tenantB);
        var currentUser = StubCurrentUser(tenantA);

        var rows = await AuditQueryFilters.ApplyAsync(
            db.AuditEntries,
            currentUser.TenantId,
            action: null,
            actorUserId: null,
            since: null,
            before: null,
            effectiveLimit: 100,
            CancellationToken.None);

        rows.ShouldAllBe(row => row.OrgId == tenantA);
        rows.Count.ShouldBe(3);
    }

    /// <summary>Given an action filter, when the query runs, then
    /// only matching rows come back.</summary>
    [Fact(DisplayName = "Given an action filter, when query runs, then only matching rows return")]
    public async Task Query_WithActionFilter_ReturnsMatchingRowsAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId);

        var rows = await AuditQueryFilters.ApplyAsync(
            db.AuditEntries,
            tenantId,
            action: AuditActions.OrgAuthProviderChanged,
            actorUserId: null,
            since: null,
            before: null,
            effectiveLimit: 100,
            CancellationToken.None);

        rows.Count.ShouldBe(1);
        rows[0].Action.ShouldBe(AuditActions.OrgAuthProviderChanged);
    }

    /// <summary>Given a since boundary, when the query runs, then
    /// rows older than the boundary are excluded.</summary>
    [Fact(DisplayName = "Given a since boundary, when query runs, then older rows are excluded")]
    public async Task Query_WithSinceFilter_ExcludesOlderRowsAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId);
        var since = new DateTimeOffset(2026, 9, 15, 11, 50, 0, TimeSpan.Zero);

        var rows = await AuditQueryFilters.ApplyAsync(
            db.AuditEntries,
            tenantId,
            action: null,
            actorUserId: null,
            since: since,
            before: null,
            effectiveLimit: 100,
            CancellationToken.None);

        // Seed used offsets -30, -15, -5 minutes from 12:00 UTC.
        // The since boundary is 11:50 (12:00 - 10min); only the
        // -5 row at 11:55 satisfies OccurredAt >= 11:50. The
        // -30 (11:30) and -15 (11:45) rows are both older.
        rows.Count.ShouldBe(1);
        rows.ShouldAllBe(row => row.OccurredAt >= since);
    }

    /// <summary>Given a limit of 1, when the query runs, then only
    /// the most recent row comes back — ordering + Take work
    /// together.</summary>
    [Fact(DisplayName = "Given a limit of 1, when query runs, then only the most recent row returns")]
    public async Task Query_WithLimit_TruncatesToLimitAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId);

        var rows = await AuditQueryFilters.ApplyAsync(
            db.AuditEntries,
            tenantId,
            action: null,
            actorUserId: null,
            since: null,
            before: null,
            effectiveLimit: 1,
            CancellationToken.None);

        rows.Count.ShouldBe(1);
        rows[0].Action.ShouldBe(AuditActions.OrgAuthProviderChanged);
    }

    /// <summary>Given an unknown action filter, when the query runs,
    /// then the result set is empty (no exception).</summary>
    [Fact(DisplayName = "Given an unknown action filter, when query runs, then the result set is empty")]
    public async Task Query_WithUnknownAction_ReturnsEmptyAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId);

        var rows = await AuditQueryFilters.ApplyAsync(
            db.AuditEntries,
            tenantId,
            action: "does.not.exist",
            actorUserId: null,
            since: null,
            before: null,
            effectiveLimit: 100,
            CancellationToken.None);

        rows.ShouldBeEmpty();
    }
}
