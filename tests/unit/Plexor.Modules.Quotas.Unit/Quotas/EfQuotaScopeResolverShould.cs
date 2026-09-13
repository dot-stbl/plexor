// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaScopeResolverShould — exercise the 3-tier scope walker against
// an in-memory QuotasDbContext. Tests the resolution rules: folder
// wins over org; org wins over default; null default returns null.
// The advisory-lock + upsert path lives on EfQuotaEnforcer and is
// covered by integration tests against real Postgres (Testcontainers).
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
///     Behavioural tests for <see cref="EfQuotaScopeResolver" />.
///     The walker is the only piece of the enforcer path that does not
///     require real Postgres (no advisory lock, no upsert); the rest of
///     the path needs Testcontainers + the real Npgsql provider.
/// </summary>
public sealed class EfQuotaScopeResolverShould
{
    private const string DefaultKey = "compute.vms.count";

    private static readonly QuotaDefinitionKey Key = QuotaDefinitionKey.VmsCount;

    /// <summary>Given a folder assignment, when resolving, then folder
    /// wins regardless of whether an org assignment also exists.</summary>
    [Fact(DisplayName = "Given folder + org assignments, when resolving folder scope, then folder wins")]
    public async Task FolderAssignmentWinsOverOrgAssignmentAsync()
    {
        var orgId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, DefaultKey, defaultValue: 100m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Org, orgId, value: 50m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Folder, folderId, value: 7m);

        var sut = new EfQuotaScopeResolver(db, new EfQuotaCatalog(db));

        var resolved = await sut.ResolveAsync(
            QuotaScope.Folder(folderId, orgId, teamId: null),
            Key);

        resolved.ShouldNotBeNull();
        var effective = resolved;
        effective.Value.ShouldBe(7m);
        effective.Origin.ShouldBe(QuotaScopeKind.Folder);
    }

    /// <summary>Given only an org assignment, when resolving, then
    /// org wins over the catalog default.</summary>
    [Fact(DisplayName = "Given org assignment only, when resolving, then org wins")]
    public async Task OrgAssignmentReturnsOrgOriginAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, DefaultKey, defaultValue: 100m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Org, orgId, value: 42m);

        var sut = new EfQuotaScopeResolver(db, new EfQuotaCatalog(db));

        var resolved = await sut.ResolveAsync(QuotaScope.Org(orgId), Key);

        resolved.ShouldNotBeNull();
        var effective = resolved;
        effective.Value.ShouldBe(42m);
        effective.Origin.ShouldBe(QuotaScopeKind.Org);
    }

    /// <summary>Given no assignment, when resolving, then the catalog
    /// default is returned.</summary>
    [Fact(DisplayName = "Given no assignment, when resolving, then returns catalog default")]
    public async Task NoAssignmentReturnsDefaultValueAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        await SeedDefinitionAsync(db, DefaultKey, defaultValue: 100m);

        var sut = new EfQuotaScopeResolver(db, new EfQuotaCatalog(db));

        var resolved = await sut.ResolveAsync(QuotaScope.Org(orgId), Key);

        resolved.ShouldNotBeNull();
        var effective = resolved;
        effective.Value.ShouldBe(100m);
        effective.Origin.ShouldBeNull();
    }

    /// <summary>Given no assignment and no default, when resolving,
    /// then null (enforcer interprets as unlimited).</summary>
    [Fact(DisplayName = "Given no assignment and no default, when resolving, then returns null")]
    public async Task NoAssignmentAndNoDefaultReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        await SeedDefinitionAsync(db, DefaultKey, defaultValue: null);

        var sut = new EfQuotaScopeResolver(db, new EfQuotaCatalog(db));

        var resolved = await sut.ResolveAsync(QuotaScope.Org(orgId), Key);

        resolved.ShouldBeNull();
    }

    /// <summary>Given an unknown catalog key, when resolving, then null
    /// (enforcer treats unknown keys as unlimited).</summary>
    [Fact(DisplayName = "Given unknown definition key, when resolving, then returns null")]
    public async Task UnknownDefinitionReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();

        var sut = new EfQuotaScopeResolver(db, new EfQuotaCatalog(db));

        var resolved = await sut.ResolveAsync(
            QuotaScope.Org(orgId),
            new QuotaDefinitionKey("does.not.exist"));

        resolved.ShouldBeNull();
    }

    /// <summary>Given a folder scope with no folder assignment but an
    /// org assignment, when resolving, then org wins (folder miss →
    /// fall through to org).</summary>
    [Fact(DisplayName = "Given folder scope with no folder assignment, when resolving, then falls through to org")]
    public async Task FolderMissFallsThroughToOrgAsync()
    {
        var orgId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        await using var db = await QuotasTestDb.CreateAsync();
        var definition = await SeedDefinitionAsync(db, DefaultKey, defaultValue: 100m);
        await SeedAssignmentAsync(db, definition.Id, QuotaScopeKind.Org, orgId, value: 25m);

        var sut = new EfQuotaScopeResolver(db, new EfQuotaCatalog(db));

        var resolved = await sut.ResolveAsync(
            QuotaScope.Folder(folderId, orgId, teamId: null),
            Key);

        resolved.ShouldNotBeNull();
        var effective = resolved;
        effective.Value.ShouldBe(25m);
        effective.Origin.ShouldBe(QuotaScopeKind.Org);
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
            Description = "Test definition",
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

    private static async Task SeedAssignmentAsync(
        QuotasDbContext db,
        Guid definitionId,
        QuotaScopeKind scopeKind,
        Guid scopeId,
        decimal value)
    {
        var assignment = new QuotaAssignment
        {
            Id = Guid.NewGuid(),
            DefinitionId = definitionId,
            ScopeKind = scopeKind,
            ScopeId = scopeId,
            OrgId = scopeKind == QuotaScopeKind.Org ? scopeId : Guid.NewGuid(),
            Value = value,
            Period = QuotaPeriod.None,
            CreatedBy = Guid.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await db.QuotaAssignments.AddAsync(assignment);
        await db.SaveChangesAsync();
    }
}
