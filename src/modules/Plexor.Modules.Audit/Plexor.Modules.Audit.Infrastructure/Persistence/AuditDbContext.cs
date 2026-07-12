// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditDbContext — EF Core context for the Audit module. Persists audit
// entries in the 'atlas' PostgreSQL schema (schema-per-module convention
// per .agents/STATE.md). All column names + the schema constant live in
// Plexor.Shared.Persistence.DatabaseInformation; nothing is hard-coded here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Audit.Domain;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

#pragma warning disable CS1591 // XML doc — see DbContext.Set<T>() for usage

public sealed class AuditDbContext : PlexorDbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Audit);
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
///     Fluent-API configuration for <see cref="AuditEntry" />. Lives in
///     Infrastructure (not Domain) because it depends on EF Core's
///     <see cref="EntityTypeBuilder{T}" />. Domain stays EF-clean.
/// </summary>
/// <remarks>
///     <para><b>Schema.</b> Set on the DbContext via
///     <see cref="PlexorDbContext" />'s <c>HasDefaultSchema</c>; not repeated
///     here. Entity configuration just describes columns + indexes.</para>
///     <para><b>snake_case.</b> Every <c>HasColumnName(...)</c> argument is
///     lowercase snake — the runtime naming convention catches anything we
///     forget, but the explicit form is the design-time contract that
///     migrations actually emit.</para>
///     <para><b>Lengths.</b> Every <c>string</c> property has
///     <c>HasMaxLength</c> — bare <c>string</c> would emit unbounded
///     <c>text</c> columns, the rule forbids that
///     (<c>coding/ef-core.md</c> §String-length).</para>
/// </remarks>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.AuditEntries);

        builder.HasKey(static entry => entry.Id);

        builder.Property(static entry => entry.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static entry => entry.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static entry => entry.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static entry => entry.Action)
            .HasColumnName("action")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static entry => entry.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static entry => entry.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid");

        builder.Property(static entry => entry.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("uuid");

        builder.Property(static entry => entry.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(static entry => entry.MetadataJson)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Indexes — read paths filter by (tenant_id, occurred_at desc)
        // and (tenant_id, resource_type). Both are explicit because
        // EF Core cannot infer composite indexes from query patterns.
        builder.HasIndex(static entry => new { entry.TenantId, entry.OccurredAt })
            .HasDatabaseName("ix_atlas_audit_entries_tenant_id_occurred_at");

        builder.HasIndex(static entry => new { entry.TenantId, entry.ResourceType })
            .HasDatabaseName("ix_atlas_audit_entries_tenant_id_resource_type");
    }
}
