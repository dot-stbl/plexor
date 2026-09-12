using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Quotas.Domain.Entities;

namespace Plexor.Modules.Quotas.Infrastructure.Persistence.Configurations;

/// <summary>
///     Snapshot table — composite primary key
///     <c>(scope_kind, scope_id, definition_id)</c> matches the
///     enforcer's per-scope lock semantics (one row per scope ×
///     definition). <c>PeriodStart</c> is part of the natural key for
///     hour-bucketed rate-limit rows but the spec lands it later; v1
///     uses the lifetime-counters shape with <c>PeriodStart = created_at</c>.
/// </summary>
internal sealed class QuotaUsageConfiguration : IEntityTypeConfiguration<QuotaUsage>
{
    public void Configure(EntityTypeBuilder<QuotaUsage> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.QuotaUsage);

        // Composite PK on (scope_kind, scope_id, definition_id). The
        // enforcer upserts one row per (scope, definition) and the
        // composite PK is the natural lookup key.
        builder.HasKey(static usage => new
        {
            usage.ScopeKind,
            usage.ScopeId,
            usage.DefinitionId,
        });

        // scope_kind is part of the PK — store as string so the
        // column has a bounded width.
        builder.Property(static usage => usage.ScopeKind)
            .HasColumnName("scope_kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static usage => usage.ScopeId)
            .HasColumnName("scope_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static usage => usage.DefinitionId)
            .HasColumnName("definition_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static usage => usage.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static usage => usage.CurrentValue)
            .HasColumnName("current_value")
            .HasColumnType("numeric(38,18)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(static usage => usage.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(static usage => usage.LastReconciledAt)
            .HasColumnName("last_reconciled_at")
            .IsRequired();

        builder.Property(static usage => usage.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static usage => usage.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Tenant-scoped usage queries (the dashboard's "where am I
        // against the limit" view).
        builder.HasIndex(static usage => usage.OrgId)
            .HasDatabaseName("ix_quotas_quota_usage_org_id");

        // FK to QuotaDefinition — explicit declaration because the
        // Definition entity's navigation is not exposed.
        builder.HasOne<QuotaDefinition>()
            .WithMany()
            .HasForeignKey(static usage => usage.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
