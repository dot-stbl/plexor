// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOrgSeederShould — exercise the 4.5.f org-default-assignment seeder
// against an in-memory QuotasDbContext. The seeder is the read + insert
// pair: read catalog + existing org-scoped assignments, compute the
// set difference, insert the missing rows. Tests cover the idempotency
// invariants that the hosted service in Plexor.Host / Plexor.Migrator
// depends on (re-runs must be no-ops).
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
///     Behavioural tests for <see cref="EfOrgSeeder" />. Uses the
///     in-memory provider (via <see cref="QuotasTestDb" />); the
///     Postgres UNIQUE constraint is not exercised here — the
///     set-difference query is the idempotency boundary in unit tests.
/// </summary>
public sealed class EfOrgSeederShould
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    /// <summary>Given a fresh org, when SeedOrgAsync runs, then one
    /// QuotaAssignment row is inserted for every catalog key with a
    /// non-null DefaultValue.</summary>
    [Fact(DisplayName = "Given a fresh org, when SeedOrgAsync runs, then one row per catalog default")]
    public async Task SeedOrgAsync_WithFreshOrg_InsertsAllDefinitionsAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        await SeedCatalogAsync(db, ("compute.vms.count", QuotaUnit.Count, QuotaPeriod.None, 100m),
                                   ("api.requests.per_hour.user", QuotaUnit.ReqPerHour, QuotaPeriod.Hour, 1000m),
                                   ("compute.vms.vcpu", QuotaUnit.Vcpu, QuotaPeriod.None, 256m));

        var sut = new EfOrgSeeder(db, Clock);

        var inserted = await sut.SeedOrgAsync(orgId);

        inserted.ShouldBe(3);
        var assignments = await ListOrgAssignmentsAsync(db, orgId);
        assignments.Count.ShouldBe(3);
        var values = assignments.Select(static assignment => assignment.Value).ToList();
        values.ShouldContain(100m);
        values.ShouldContain(256m);
        values.ShouldContain(1000m);
        foreach (var assignment in assignments)
        {
            assignment.CreatedBy.ShouldBe(Guid.Empty);
            assignment.OrgId.ShouldNotBe(Guid.Empty);
        }
    }

    /// <summary>Given an org whose assignments already cover every
    /// catalog key, when SeedOrgAsync runs again, then 0 rows are
    /// inserted and existing assignments are untouched.</summary>
    [Fact(DisplayName = "Given an already-seeded org, when SeedOrgAsync runs, then inserts nothing")]
    public async Task SeedOrgAsync_WithAlreadySeededOrg_SkipsExistingAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedCatalogAsync(db,
            ("compute.vms.count", QuotaUnit.Count, QuotaPeriod.None, 100m));
        // Pre-seed the org with one assignment.
        await SeedAssignmentAsync(db, definition.Id, orgId, value: 42m);
        var sut = new EfOrgSeeder(db, Clock);

        var inserted = await sut.SeedOrgAsync(orgId);

        inserted.ShouldBe(0);
        var assignments = await ListOrgAssignmentsAsync(db, orgId);
        assignments.Count.ShouldBe(1);
        assignments[0].Value.ShouldBe(42m);
    }

    /// <summary>Given a catalog row with DefaultValue = null, when
    /// SeedOrgAsync runs, then no assignment is created for that key
    /// (seeding a row with no value would defeat the purpose).</summary>
    [Fact(DisplayName = "Given a catalog key with null DefaultValue, when seeding, then the key is skipped")]
    public async Task SeedOrgAsync_WithNullDefaultValue_SkipsThatDefinitionAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        await SeedCatalogAsync(db, ("compute.vms.count", QuotaUnit.Count, QuotaPeriod.None, 100m),
                                   ("compute.unknown.count", QuotaUnit.Count, QuotaPeriod.None, null));

        var sut = new EfOrgSeeder(db, Clock);

        var inserted = await sut.SeedOrgAsync(orgId);

        inserted.ShouldBe(1);
        var assignments = await ListOrgAssignmentsAsync(db, orgId);
        assignments.Count.ShouldBe(1);
        assignments[0].DefinitionId.ShouldNotBe(Guid.Empty);
    }

    /// <summary>Given multiple orgs, when SeedAllOrgsAsync runs, then
    /// each org is seeded independently and a re-run on the same
    /// collection is a no-op.</summary>
    [Fact(DisplayName = "Given multiple orgs, when SeedAllOrgsAsync runs, then each org is seeded; re-run is no-op")]
    public async Task SeedAllOrgsAsync_WithMultipleOrgs_HandlesEachAsync()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        await SeedCatalogAsync(db, ("compute.vms.count", QuotaUnit.Count, QuotaPeriod.None, 100m),
                                   ("compute.vms.vcpu", QuotaUnit.Vcpu, QuotaPeriod.None, 256m));

        var sut = new EfOrgSeeder(db, Clock);

        var firstPass = await sut.SeedAllOrgsAsync([orgA, orgB]);
        var secondPass = await sut.SeedAllOrgsAsync([orgA, orgB]);

        firstPass.ShouldBe(4);
        secondPass.ShouldBe(0);
        var assignmentsA = await ListOrgAssignmentsAsync(db, orgA);
        var assignmentsB = await ListOrgAssignmentsAsync(db, orgB);
        assignmentsA.Count.ShouldBe(2);
        assignmentsB.Count.ShouldBe(2);
    }

    private static async Task<List<QuotaAssignment>> ListOrgAssignmentsAsync(
        QuotasDbContext db,
        Guid orgId)
    {
        return await db.QuotaAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.ScopeKind == QuotaScopeKind.Org
                && assignment.ScopeId == orgId)
            .ToListAsync();
    }

    private static async Task<QuotaDefinition> SeedCatalogAsync(
        QuotasDbContext db,
        params (string Key, QuotaUnit Unit, QuotaPeriod Period, decimal? DefaultValue)[] entries)
    {
        var now = DateTimeOffset.UtcNow;
        QuotaDefinition? last = null;
        foreach (var entry in entries)
        {
            var definition = new QuotaDefinition
            {
                Id = Guid.NewGuid(),
                Key = entry.Key,
                Description = $"Test {entry.Key}",
                Unit = entry.Unit,
                Period = entry.Period,
                DefaultValue = entry.DefaultValue,
                Builtin = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await db.QuotaDefinitions.AddAsync(definition);
            last = definition;
        }

        await db.SaveChangesAsync();
        return last ?? throw new InvalidOperationException("no entries");
    }

    private static async Task SeedAssignmentAsync(
        QuotasDbContext db,
        Guid definitionId,
        Guid orgId,
        decimal value)
    {
        var now = DateTimeOffset.UtcNow;
        var assignment = new QuotaAssignment
        {
            Id = Guid.NewGuid(),
            DefinitionId = definitionId,
            ScopeKind = QuotaScopeKind.Org,
            ScopeId = orgId,
            OrgId = orgId,
            Value = value,
            Period = QuotaPeriod.None,
            CreatedBy = Guid.Empty,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.QuotaAssignments.AddAsync(assignment);
        await db.SaveChangesAsync();
    }
}