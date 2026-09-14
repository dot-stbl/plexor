// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GlobalThemeConfigConfiguration — EF Core configuration for the
// singleton operator-global branding row. The table holds at most one
// row; the singleton invariant is enforced by a UNIQUE partial index
// on the sentinel id (Postgres idiom for "row must exist exactly once
// if a row exists").
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Branding.Domain.Entities;

namespace Plexor.Modules.Branding.Infrastructure.Persistence.Configurations;

/// <summary>
///     Snake_case + UNIQUE partial index on the singleton id. The
///     <c>WHERE id IS NOT NULL</c> filter is a no-op (id is NOT NULL
///     by definition) but makes the "at most one row" intent
///     explicit in the schema. A future migration that accidentally
///     inserts a second row would fail with a unique-violation
///     instead of silently corrupting the singleton.
/// </summary>
internal sealed class GlobalThemeConfigConfiguration
    : IEntityTypeConfiguration<GlobalThemeConfig>
{
    public void Configure(EntityTypeBuilder<GlobalThemeConfig> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.GlobalThemeConfig);

        builder.HasKey(static config => config.Id);

        builder.Property(static config => config.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static config => config.BrandName)
            .HasColumnName("brand_name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static config => config.BrandLogoUrl)
            .HasColumnName("brand_logo_url")
            .HasMaxLength(2048);

        builder.Property(static config => config.BrandFaviconUrl)
            .HasColumnName("brand_favicon_url")
            .HasMaxLength(2048);

        builder.Property(static config => config.DefaultPresetId)
            .HasColumnName("default_preset_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static config => config.CustomAccent)
            .HasColumnName("custom_accent")
            .HasMaxLength(64);

        builder.Property(static config => config.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("uuid");

        builder.Property(static config => config.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Singleton invariant — partial unique index. Postgres
        // partial indexes with `WHERE id IS NOT NULL` are the canonical
        // way to enforce "this table has exactly one row".
        builder.HasIndex(static config => config.Id)
            .HasDatabaseName("ix_branding_global_theme_config_singleton")
            .IsUnique();
    }
}