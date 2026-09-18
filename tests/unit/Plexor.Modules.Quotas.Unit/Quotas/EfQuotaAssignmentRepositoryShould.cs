// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaAssignmentRepositoryShould — exercise the 4.5.g.2 read + write
// surface for QuotaAssignment against an in-memory QuotasDbContext.
//
// The read paths (ListForScopeAsync, FindAsync) drive the GET
// /api/v1/quotas/assignments endpoint; the write paths (UpsertAsync,
// DeleteAsync) are declared now so 4.5.g.3 can land its PUT / DELETE
// handlers without an interface churn. All four methods get unit tests
// here against the InMemory provider — the Postgres-specific advisory-
// lock path lives on IQuotaEnforcer and is not under test here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Modules.Quotas.Infrastructure.Quotas;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Quotas.Unit.Quotas;

/// <summary>
///     Behavioural tests for <see cref="EfQuotaAssignmentRepository" />.
///     The Postgres UNIQUE constraint is not exercised here — the
///     query filters are the contract under test. The InMemory
///     provider does not enforce the composite UNIQUE index, so the
///     "insert when missing, update when present" branch is asserted
///     by row count + value equality, not by catching
///     <see cref="DbUpdateException" />.
/// </summary>
public sealed class EfQuotaAssignmentRepositoryShould
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    /// <summary>Given three assignments across two scopes, when listing
    /// one scope, then only that scope's rows come back.</summary>
    [Fact(DisplayName = "Given assignments across two scopes, when listing one scope, then only matching rows return")]
    public async Task ListForScopeAsync_ReturnsOnlyMatchingScopeAsync()
    {
        var orgId = Guid.NewGuid();
        var scopeA = Guid.NewGuid();
        var scopeB = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, "compute.vms.count", defaultValue: 100m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Folder, scopeA, orgId, value: 7m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Folder, scopeB, orgId, value: 9m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Org, orgId, orgId, value: 100m);

        var sut = new EfQuotaAssignmentRepository(db, Clock);

        var rows = await sut.ListForScopeAsync(QuotaScopeKind.Folder, scopeA, orgId, CancellationToken.None);

        rows.Count.ShouldBe(1);
        rows[0].ScopeId.ShouldBe(scopeA);
        rows[0].Value.ShouldBe(7m);
    }

    /// <summary>Given two assignments at the same scope but different
    /// orgs, when listing one org, then only that org's row returns.</summary>
    [Fact(DisplayName = "Given assignments at same scope + different orgs, when listing one org, then only that org returns")]
    public async Task ListForScopeAsync_FiltersByOrgIdAsync()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var scope = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, "compute.vms.count", defaultValue: 100m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Folder, scope, orgA, value: 7m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Folder, scope, orgB, value: 9m);

        var sut = new EfQuotaAssignmentRepository(db, Clock);

        var orgARows = await sut.ListForScopeAsync(QuotaScopeKind.Folder, scope, orgA, CancellationToken.None);
        var orgBRows = await sut.ListForScopeAsync(QuotaScopeKind.Folder, scope, orgB, CancellationToken.None);

        orgARows.Count.ShouldBe(1);
        orgARows[0].OrgId.ShouldBe(orgA);
        orgARows[0].Value.ShouldBe(7m);
        orgBRows.Count.ShouldBe(1);
        orgBRows[0].OrgId.ShouldBe(orgB);
        orgBRows[0].Value.ShouldBe(9m);
    }

    /// <summary>Given an existing assignment id, when FindAsync is
    /// called with that id, then the row returns.</summary>
    [Fact(DisplayName = "Given an existing assignment, when FindAsync runs, then the row returns")]
    public async Task FindAsync_WithExistingId_ReturnsRowAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, "compute.vms.count", defaultValue: 100m);
        var seeded = await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Org, orgId, orgId, value: 42m);

        var sut = new EfQuotaAssignmentRepository(db, Clock);

        var row = await sut.FindAsync(seeded.Id, CancellationToken.None);

        row.ShouldNotBeNull();
        row.Id.ShouldBe(seeded.Id);
        row.Value.ShouldBe(42m);
    }

    /// <summary>Given an unknown id, when FindAsync runs, then null
    /// returns (no exception).</summary>
    [Fact(DisplayName = "Given an unknown id, when FindAsync runs, then returns null")]
    public async Task FindAsync_WithMissingId_ReturnsNullAsync()
    {
        await using var db = await QuotasTestDb.CreateAsync();

        var sut = new EfQuotaAssignmentRepository(db, Clock);

        var row = await sut.FindAsync(Guid.NewGuid(), CancellationToken.None);

        row.ShouldBeNull();
    }

    /// <summary>Given no existing row, when UpsertAsync runs, then a
    /// new row inserts with the supplied values.</summary>
    [Fact(DisplayName = "Given no existing row, when UpsertAsync runs, then a new row inserts")]
    public async Task UpsertAsync_WithNewRow_InsertsAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, "compute.vms.count", defaultValue: 100m);

        var sut = new EfQuotaAssignmentRepository(db, Clock);

        var result = await sut.UpsertAsync(
            definition.Id,
            QuotaScope.Org(orgId),
            value: 50m,
            QuotaPeriod.None,
            createdBy: Guid.NewGuid(),
            CancellationToken.None);

        result.Id.ShouldNotBe(Guid.Empty);
        result.DefinitionId.ShouldBe(definition.Id);
        result.ScopeKind.ShouldBe(QuotaScopeKind.Org);
        result.ScopeId.ShouldBe(orgId);
        result.OrgId.ShouldBe(orgId);
        result.Value.ShouldBe(50m);
        result.Period.ShouldBe(QuotaPeriod.None);
        result.CreatedAt.ShouldNotBe(default);

        var stored = await db.QuotaAssignments
            .AsNoTracking()
            .SingleAsync(assignment => assignment.Id == result.Id);
        stored.Value.ShouldBe(50m);
    }

    /// <summary>Given an existing row at the same
    /// (definition, scope, period), when UpsertAsync runs with a
    /// different value, then the value + UpdatedAt change and
    /// CreatedAt / CreatedBy are preserved.</summary>
    [Fact(DisplayName = "Given an existing row, when UpsertAsync runs with a different value, then value + UpdatedAt change and CreatedAt / CreatedBy are preserved")]
    public async Task UpsertAsync_WithExistingRow_UpdatesValueAsync()
    {
        var orgId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, "compute.vms.count", defaultValue: 100m);

        var first = await new EfQuotaAssignmentRepository(db, Clock).UpsertAsync(
            definition.Id,
            QuotaScope.Org(orgId),
            value: 50m,
            QuotaPeriod.None,
            createdBy: actor,
            CancellationToken.None);

        // Wait one tick so UpdatedAt can advance past CreatedAt.
        await Task.Delay(10);

        var second = await new EfQuotaAssignmentRepository(db, Clock).UpsertAsync(
            definition.Id,
            QuotaScope.Org(orgId),
            value: 75m,
            QuotaPeriod.None,
            createdBy: actor,
            CancellationToken.None);

        second.Id.ShouldBe(first.Id);
        second.Value.ShouldBe(75m);
        second.CreatedAt.ShouldBe(first.CreatedAt);
        second.CreatedBy.ShouldBe(actor);
        second.UpdatedAt.ShouldBeGreaterThan(first.UpdatedAt);
    }

    /// <summary>Given an existing row, when DeleteAsync runs with the
    /// row's id, then the row is gone.</summary>
    [Fact(DisplayName = "Given an existing row, when DeleteAsync runs, then the row is gone")]
    public async Task DeleteAsync_RemovesRowAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, "compute.vms.count", defaultValue: 100m);
        var seeded = await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Org, orgId, orgId, value: 42m);

        var sut = new EfQuotaAssignmentRepository(db, Clock);

        await sut.DeleteAsync(seeded.Id, CancellationToken.None);

        var row = await db.QuotaAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(assignment => assignment.Id == seeded.Id);
        row.ShouldBeNull();
    }

    /// <summary>Given an unknown id, when DeleteAsync runs, then the
    /// call is a no-op (no exception, no rows removed).</summary>
    [Fact(DisplayName = "Given an unknown id, when DeleteAsync runs, then the call is a no-op")]
    public async Task DeleteAsync_WithMissingId_IsNoOpAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, "compute.vms.count", defaultValue: 100m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Org, orgId, orgId, value: 42m);

        var sut = new EfQuotaAssignmentRepository(db, Clock);

        await sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        var remaining = await db.QuotaAssignments.CountAsync();
        remaining.ShouldBe(1);
    }

    private static async Task<QuotaDefinition> SeedDefinitionAsync(
        QuotasDbContext db,
        string key,
        decimal? defaultValue)
    {
        var definition = new QuotaDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            Description = $"Test {key}",
            Unit = QuotaUnit.Count,
            Period = QuotaPeriod.None,
            DefaultValue = defaultValue,
            Builtin = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await db.QuotaDefinitions.AddAsync(definition);
        await db.SaveChangesAsync();
        return definition;
    }

    private static async Task<QuotaAssignment> SeedAssignmentAsync(
        QuotasDbContext db,
        Guid definitionId,
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        decimal value)
    {
        var now = DateTimeOffset.UtcNow;
        var assignment = new QuotaAssignment
        {
            Id = Guid.NewGuid(),
            DefinitionId = definitionId,
            ScopeKind = scopeKind,
            ScopeId = scopeId,
            OrgId = orgId,
            Value = value,
            Period = QuotaPeriod.None,
            CreatedBy = Guid.Empty,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.QuotaAssignments.AddAsync(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }
}
