// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaUsageReaderShould — exercise the 4.5.g.2 read-only surface for
// QuotaUsage against an in-memory QuotasDbContext. The reader powers
// GET /api/v1/quotas/usage, which pairs each snapshot with the resolved
// effective limit; tests here cover the row-level filter contract.
// ============================================================================

using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Modules.Quotas.Infrastructure.Quotas;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Quotas.Unit.Quotas;

/// <summary>
///     Behavioural tests for <see cref="EfQuotaUsageReader" />. The
///     writer is the 4.5.b enforcer (out of scope here); the reader is
///     read-only and the filter contract is what the controller
///     depends on.
/// </summary>
public sealed class EfQuotaUsageReaderShould
{
    /// <summary>Given usage rows at one scope, when reading that scope,
    /// then every matching row returns.</summary>
    [Fact(DisplayName = "Given usage rows at one scope, when reading that scope, then all matching rows return")]
    public async Task ListForScopeAsync_ReturnsMatchingRowsAsync()
    {
        var orgId = Guid.NewGuid();
        var scope = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var defA = await SeedDefinitionAsync(db, "compute.vms.count");
        var defB = await SeedDefinitionAsync(db, "compute.vms.vcpu");
        await SeedUsageAsync(db, defA.Id, QuotaScopeKind.Folder, scope, orgId, value: 3m);
        await SeedUsageAsync(db, defB.Id, QuotaScopeKind.Folder, scope, orgId, value: 24m);

        var sut = new EfQuotaUsageReader(db);

        var rows = await sut.ListForScopeAsync(QuotaScopeKind.Folder, scope, orgId, CancellationToken.None);

        rows.Count.ShouldBe(2);
        rows.Select(static usage => usage.DefinitionId)
            .ShouldBe([defA.Id, defB.Id], ignoreOrder: true);
    }

    /// <summary>Given usage rows at the same scope across two orgs,
    /// when reading one org, then only that org's rows return.</summary>
    [Fact(DisplayName = "Given usage rows at same scope + different orgs, when reading one org, then only that org's rows return")]
    public async Task ListForScopeAsync_FiltersByOrgIdAsync()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var scope = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        // QuotaUsage has a composite PK (ScopeKind, ScopeId, DefinitionId),
        // so two orgs sharing a scope must reference different catalog
        // entries to satisfy the constraint.
        var defA = await SeedDefinitionAsync(db, "compute.vms.count");
        var defB = await SeedDefinitionAsync(db, "compute.vms.vcpu");
        await SeedUsageAsync(db, defA.Id, QuotaScopeKind.Folder, scope, orgA, value: 3m);
        await SeedUsageAsync(db, defB.Id, QuotaScopeKind.Folder, scope, orgB, value: 9m);

        var sut = new EfQuotaUsageReader(db);

        var orgARows = await sut.ListForScopeAsync(QuotaScopeKind.Folder, scope, orgA, CancellationToken.None);
        var orgBRows = await sut.ListForScopeAsync(QuotaScopeKind.Folder, scope, orgB, CancellationToken.None);

        orgARows.Count.ShouldBe(1);
        orgARows[0].OrgId.ShouldBe(orgA);
        orgARows[0].CurrentValue.ShouldBe(3m);
        orgBRows.Count.ShouldBe(1);
        orgBRows[0].OrgId.ShouldBe(orgB);
        orgBRows[0].CurrentValue.ShouldBe(9m);
    }

    private static async Task<QuotaDefinition> SeedDefinitionAsync(
        QuotasDbContext db,
        string key)
    {
        var definition = new QuotaDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            Description = $"Test {key}",
            Unit = QuotaUnit.Count,
            Period = QuotaPeriod.None,
            DefaultValue = 100m,
            Builtin = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await db.QuotaDefinitions.AddAsync(definition);
        await db.SaveChangesAsync();
        return definition;
    }

    private static async Task SeedUsageAsync(
        QuotasDbContext db,
        Guid definitionId,
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        decimal value)
    {
        var now = DateTimeOffset.UtcNow;
        var usage = new QuotaUsage
        {
            ScopeKind = scopeKind,
            ScopeId = scopeId,
            DefinitionId = definitionId,
            OrgId = orgId,
            CurrentValue = value,
            PeriodStart = now,
            LastReconciledAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.QuotaUsage.AddAsync(usage);
        await db.SaveChangesAsync();
    }
}
