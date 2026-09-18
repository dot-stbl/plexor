// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingDbContext — EF Core context for the Branding module. Owns
// global_theme_config (singleton) + org_theme_config (per-org) in the
// `branding` PostgreSQL schema (architecture theme). The schema name
// + table names live in the module-local DatabaseInformation; nothing
// is hard-coded here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Branding.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Branding module. Persists the
///     operator-global and per-org branding rows in the
///     <c>branding</c> PostgreSQL schema. Composed from the two
///     <see cref="IEntityTypeConfiguration{TEntity}" /> files in
///     <c>Configurations/</c>.
/// </summary>
/// <param name="options">EF Core options bag.</param>
public sealed class BrandingDbContext(DbContextOptions<BrandingDbContext> options)
    : PlexorDbContext(options)
{
    /// <summary>GlobalThemeConfig (branding.global_theme_config) —
    /// singleton row of operator branding defaults.</summary>
    public DbSet<GlobalThemeConfig> GlobalThemeConfig => Set<GlobalThemeConfig>();

    /// <summary>OrgThemeConfig (branding.org_theme_config) — one row
    /// per org with optional per-tenant overrides.</summary>
    public DbSet<OrgThemeConfig> OrgThemeConfig => Set<OrgThemeConfig>();

    /// <summary>ThemeInstallation (branding.theme_installations) —
    /// one row per org recording the marketplace theme the
    /// operator has activated. Distinct from OrgThemeConfig because
    /// it carries the publisher-signed manifest + signature.</summary>
    public DbSet<ThemeInstallation> ThemeInstallations => Set<ThemeInstallation>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Branding)
            .ApplyConfiguration(new GlobalThemeConfigConfiguration())
            .ApplyConfiguration(new OrgThemeConfigConfiguration())
            .ApplyConfiguration(new ThemeInstallationConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}