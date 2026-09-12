using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Quotas.Domain.Entities;

namespace Plexor.Modules.Quotas.Infrastructure.Persistence.Configurations;

/// <summary>
///     Snake_case + UNIQUE composite on
///     <c>(definition_id, scope_kind, scope_id, period)</c> — enforces
///     the "exactly one assignment per (scope, definition, period)"
///     invariant from the spec.
/// </summary>
internal sealed class QuotaAssignmentConfiguration : IEntityTypeConfiguration<QuotaAssignment>
{
    public void Configure(EntityTypeBuilder<QuotaAssignment> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.QuotaAssignments);

        builder.HasKey(static assignment => assignment.Id);

        builder.Property(static assignment => assignment.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static assignment => assignment.DefinitionId)
            .HasColumnName("definition_id")
            .HasColumnType("uuid")
            .IsRequired();

        // ScopeKind stored as string (forward-compatible enum).
        builder.Property(static assignment => assignment.ScopeKind)
            .HasColumnName("scope_kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static assignment => assignment.ScopeId)
            .HasColumnName("scope_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static assignment => assignment.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static assignment => assignment.Value)
            .HasColumnName("value")
            .HasColumnType("numeric(38,18)")
            .IsRequired();

        // Period override — None by default; non-None replaces the
        // catalog period for this assignment only.
        builder.Property(static assignment => assignment.Period)
            .HasColumnName("period")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static assignment => assignment.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static assignment => assignment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static assignment => assignment.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Composite UNIQUE — the spec invariant "exactly one row per
        // (definition, scope_kind, scope_id, period)". Period is
        // included so a future absolute-vs-rate-limit split for the
        // same key can coexist without losing the absolute row.
        builder.HasIndex(static assignment => new
        {
            assignment.DefinitionId,
            assignment.ScopeKind,
            assignment.ScopeId,
            assignment.Period,
        })
            .HasDatabaseName("ix_quotas_quota_assignments_definition_scope_period")
            .IsUnique();

        // Tenant-scoped assignment list queries (the org's assignments
        // for the dashboard).
        builder.HasIndex(static assignment => assignment.OrgId)
            .HasDatabaseName("ix_quotas_quota_assignments_org_id");

        // FK to QuotaDefinition — explicit declaration because the
        // Definition entity's navigation is not exposed (the catalog
        // is read-only from the resolver perspective).
        builder.HasOne<QuotaDefinition>()
            .WithMany()
            .HasForeignKey(static assignment => assignment.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
