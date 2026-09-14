// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgThemeConfigConfiguration — EF Core configuration for the
// per-org branding override row. UNIQUE on org_id enforces the
// invariant "at most one override row per realm.organizations.id";
// no FK because the Branding module doesn't take a dependency on
// the Realm module (the controller validates the orgId against
// ICurrentUser at runtime).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Branding.Domain.Entities;

namespace Plexor.Modules.Branding.Infrastructure.Persistence.Configurations;

/// <summary>
///     Snake_case + UNIQUE on <c>org_id</c>. The Branding module
///     intentionally does NOT declare an FK into
///     <c>realm.organizations</c> — the controller validates the
///     supplied orgId against <c>ICurrentUser.OrgId</c> at runtime,
///     and a hard FK would force a Realm dependency on the Branding
///     module that the architecture doesn't want.
/// </summary>
internal sealed class OrgThemeConfigConfiguration
    : IEntityTypeConfiguration<OrgThemeConfig>
{
    public void Configure(EntityTypeBuilder<OrgThemeConfig> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.OrgThemeConfig);

        builder.HasKey(static config => config.Id);

        builder.Property(static config => config.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static config => config.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static config => config.PresetId)
            .HasColumnName("preset_id")
            .HasMaxLength(64);

        builder.Property(static config => config.CustomAccent)
            .HasColumnName("custom_accent")
            .HasMaxLength(64);

        builder.Property(static config => config.BrandName)
            .HasColumnName("brand_name")
            .HasMaxLength(128);

        builder.Property(static config => config.BrandLogoUrl)
            .HasColumnName("brand_logo_url")
            .HasMaxLength(2048);

        builder.Property(static config => config.BrandFaviconUrl)
            .HasColumnName("brand_favicon_url")
            .HasMaxLength(2048);

        builder.Property(static config => config.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("uuid");

        builder.Property(static config => config.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(static config => config.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // One override row per org. Lookup-by-orgId is the hot path
        // (every boot-config request resolves the tenant override).
        builder.HasIndex(static config => config.OrgId)
            .HasDatabaseName("ix_branding_org_theme_config_org_id")
            .IsUnique();
    }
}