// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOrgAuthProviderSeederShould — exercise the 4.6.1 first-boot
// seeder against an in-memory RealmDbContext. Tests cover the
// idempotency invariants the host + migrator depend on (re-runs
// must be no-ops).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.AuthProviders;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Realm.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for
///     <see cref="EfOrgAuthProviderSeeder" />. Uses the in-memory
///     provider (via <see cref="RealmTestDb" />); the Postgres
///     UNIQUE constraint on <c>org_id</c> is not exercised here —
///     the set-difference query is the idempotency boundary in
///     unit tests.
/// </summary>
public sealed class EfOrgAuthProviderSeederShould
{
    /// <summary>The default OIDC scope list mirrored from the
    /// controller's static field — used by the assertion
    /// checks below.</summary>
    private static readonly string[] DefaultScopes = ["openid", "profile", "email"];

    /// <summary>Given no orgs at all, when SeedAllOrgsAsync runs,
    /// then no rows are inserted.</summary>
    [Fact(DisplayName = "Given no orgs, when SeedAllOrgsAsync runs, then inserts nothing")]
    public async Task SeedAllOrgsAsync_WithNoOrgs_InsertsNothingAsync()
    {
        await using var db = await RealmTestDb.CreateAsync();

        var sut = new EfOrgAuthProviderSeeder(db, TimeProvider.System);

        var inserted = await sut.SeedAllOrgsAsync(CancellationToken.None);

        inserted.ShouldBe(0);
        var count = await db.OrgAuthProviderConfigs.CountAsync();
        count.ShouldBe(0);
    }

    /// <summary>Given orgs without a config row, when
    /// SeedAllOrgsAsync runs, then one default Sigil row per
    /// missing org is inserted.</summary>
    [Fact(DisplayName = "Given orgs without configs, when SeedAllOrgsAsync runs, then default Sigil rows are inserted")]
    public async Task SeedAllOrgsAsync_WithOrgsNoConfigs_InsertsSigilDefaultsAsync()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await using var db = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(db, orgA);
        await SeedOrgAsync(db, orgB);

        var sut = new EfOrgAuthProviderSeeder(db, TimeProvider.System);

        var inserted = await sut.SeedAllOrgsAsync(CancellationToken.None);

        inserted.ShouldBe(2);
        var rows = await db.OrgAuthProviderConfigs.AsNoTracking().ToListAsync();
        rows.Count.ShouldBe(2);
        foreach (var row in rows)
        {
            row.Provider.ShouldBe(OrgAuthProvider.Sigil);
            row.OidcScopes.ShouldBe(DefaultScopes);
            row.OidcAuthority.ShouldBeNull();
            row.OidcClientId.ShouldBeNull();
            row.OidcClientSecretProtected.ShouldBeNull();
            row.CreatedAt.ShouldNotBe(default);
            row.UpdatedAt.ShouldBe(row.CreatedAt);
        }
    }

    /// <summary>Given every org already has a config row, when
    /// SeedAllOrgsAsync runs again, then no rows are inserted
    /// and the existing rows are untouched.</summary>
    [Fact(DisplayName = "Given every org already configured, when SeedAllOrgsAsync runs again, then inserts nothing")]
    public async Task SeedAllOrgsAsync_WithAllOrgsConfigured_SkipsAsync()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await using var db = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(db, orgA);
        await SeedOrgAsync(db, orgB);
        var sut = new EfOrgAuthProviderSeeder(db, TimeProvider.System);
        var firstPass = await sut.SeedAllOrgsAsync(CancellationToken.None);
        firstPass.ShouldBe(2);

        // Second pass — nothing should change.
        var secondPass = await sut.SeedAllOrgsAsync(CancellationToken.None);

        secondPass.ShouldBe(0);
        var rows = await db.OrgAuthProviderConfigs.AsNoTracking().ToListAsync();
        rows.Count.ShouldBe(2);
    }

    /// <summary>Given a mixed fleet (some orgs have configs,
    /// some don't), when SeedAllOrgsAsync runs, then only the
    /// missing orgs get a default row.</summary>
    [Fact(DisplayName = "Given a mixed fleet, when SeedAllOrgsAsync runs, then only missing orgs get a row")]
    public async Task SeedAllOrgsAsync_WithMixedFleet_InsertsOnlyMissingAsync()
    {
        var orgConfigured = Guid.NewGuid();
        var orgMissing = Guid.NewGuid();
        await using var db = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(db, orgConfigured);
        await SeedOrgAsync(db, orgMissing);
        var sut = new EfOrgAuthProviderSeeder(db, TimeProvider.System);

        // Pre-seed the first org's config (e.g. an admin flipped it
        // to OIDC manually before the seeder's first run).
        var now = DateTimeOffset.UtcNow;
        await db.OrgAuthProviderConfigs.AddAsync(new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = orgConfigured,
            Provider = OrgAuthProvider.Oidc,
            OidcAuthority = "https://kc.example.com/realms/plexor",
            OidcClientId = "plexor-console",
            OidcClientSecretProtected = "encrypted-blob",
            OidcScopes = ["openid", "profile", "email", "groups"],
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var inserted = await sut.SeedAllOrgsAsync(CancellationToken.None);

        inserted.ShouldBe(1);
        var rows = await db.OrgAuthProviderConfigs.AsNoTracking().ToListAsync();
        rows.Count.ShouldBe(2);

        var configuredRow = rows.Single(row => row.OrgId == orgConfigured);
        configuredRow.Provider.ShouldBe(OrgAuthProvider.Oidc);
        configuredRow.OidcAuthority.ShouldBe("https://kc.example.com/realms/plexor");

        var newRow = rows.Single(row => row.OrgId == orgMissing);
        newRow.Provider.ShouldBe(OrgAuthProvider.Sigil);
        newRow.OidcAuthority.ShouldBeNull();
    }

    private static async Task SeedOrgAsync(RealmDbContext db, Guid orgId)
    {
        var now = DateTimeOffset.UtcNow;
        var slug = $"org-{orgId.ToString()[..6]}";
        await db.Organizations.AddAsync(new Organization
        {
            Id = orgId,
            Name = slug,
            Slug = slug,
            Status = "active",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
    }
}
