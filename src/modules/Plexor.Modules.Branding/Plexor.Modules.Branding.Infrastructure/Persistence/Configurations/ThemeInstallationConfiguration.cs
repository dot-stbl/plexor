// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeInstallationConfiguration — EF Core configuration for the
// per-org theme-marketplace installation row. UNIQUE on `org_id`
// enforces the invariant "at most one marketplace installation per
// realm.organizations.id"; the PUT endpoint upserts in place.
// snake_case + bounded strings per Plexor conventions.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Branding.Domain.Entities;

namespace Plexor.Modules.Branding.Infrastructure.Persistence.Configurations;

/// <summary>
///     Snake_case + UNIQUE on <c>org_id</c>. The Branding module
///     intentionally does NOT declare an FK into
///     <c>realm.organizations</c> — the controller validates the
///     supplied orgId against <c>ICurrentUser.TenantId</c> at
///     runtime, and a hard FK would force a Realm dependency on
///     the Branding module the architecture doesn't want.
/// </summary>
internal sealed class ThemeInstallationConfiguration
    : IEntityTypeConfiguration<ThemeInstallation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ThemeInstallation> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.ThemeInstallations);

        builder.HasKey(static installation => installation.Id);

        builder.Property(static installation => installation.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static installation => installation.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static installation => installation.ThemeId)
            .HasColumnName("theme_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static installation => installation.ManifestSignature)
            .HasColumnName("manifest_signature")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(static installation => installation.ActivatedAt)
            .HasColumnName("activated_at")
            .IsRequired();

        builder.Property(static installation => installation.ActivatedBy)
            .HasColumnName("activated_by")
            .HasColumnType("uuid")
            .IsRequired();

        // One marketplace installation per org. Same invariant as
        // `org_theme_config` (different row, same lookup pattern);
        // index name mirrors the existing convention so the schema
        // stays predictable.
        builder.HasIndex(static installation => installation.OrgId)
            .HasDatabaseName("ix_branding_theme_installations_org_id")
            .IsUnique();
    }
}
