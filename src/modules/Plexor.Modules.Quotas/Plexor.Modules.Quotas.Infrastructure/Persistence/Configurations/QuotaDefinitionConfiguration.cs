using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Quotas.Domain.Entities;

namespace Plexor.Modules.Quotas.Infrastructure.Persistence.Configurations;

/// <summary>
///     Snake_case column names + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The catalog is global
///     (no <c>org_id</c>); uniqueness is on <c>key</c>.
/// </summary>
internal sealed class QuotaDefinitionConfiguration : IEntityTypeConfiguration<QuotaDefinition>
{
    public void Configure(EntityTypeBuilder<QuotaDefinition> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.QuotaDefinitions);

        builder.HasKey(static definition => definition.Id);

        builder.Property(static definition => definition.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        // Stable catalog identifier (e.g. "compute.vms.count").
        // The spec calls for catalog keys up to ~64 chars
        // ("compute.vms.count" = 17, with headroom for new groups).
        builder.Property(static definition => definition.Key)
            .HasColumnName("key")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static definition => definition.Description)
            .HasColumnName("description")
            .HasMaxLength(256);

        // Unit + Period stored as varchar so a future member addition
        // does not require a schema migration.
        builder.Property(static definition => definition.Unit)
            .HasColumnName("unit")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static definition => definition.Period)
            .HasColumnName("period")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static definition => definition.DefaultValue)
            .HasColumnName("default_value")
            .HasColumnType("numeric(38,18)");

        builder.Property(static definition => definition.Builtin)
            .HasColumnName("builtin")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(static definition => definition.EffectiveFrom)
            .HasColumnName("effective_from");

        builder.Property(static definition => definition.EffectiveUntil)
            .HasColumnName("effective_until");

        builder.Property(static definition => definition.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static definition => definition.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Catalog keys are globally unique. Renaming a key is a
        // breaking change requiring a migration step.
        builder.HasIndex(static definition => definition.Key)
            .HasDatabaseName("ix_quotas_quota_definitions_key")
            .IsUnique();
    }
}
