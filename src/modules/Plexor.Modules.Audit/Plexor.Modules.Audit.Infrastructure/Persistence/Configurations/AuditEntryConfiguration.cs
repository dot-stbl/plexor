using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Audit.Infrastructure.Persistence.Configurations;

/// <summary>
///     EF Core configuration for <see cref="AuditEntryRecord" />.
///     snake_case columns, <c>jsonb</c> for metadata, indexed for
///     the audit query hot paths (OrgId, actor, action, time).
/// </summary>
/// <remarks>
///     <para><b>Append-only.</b> No index targets UPDATE / DELETE —
///     the migration REVOKEs those rights at the database level so
///     even a future <c>ExecuteUpdate</c> won't succeed. Indexes
///     cover the read patterns:
///     <list type="bullet">
///         <item><c>org_id</c> — every audit query scopes by tenant.</item>
///         <item><c>actor_id + occurred_at</c> — <c>QueryByActorAsync</c>.</item>
///         <item><c>occurred_at DESC</c> — <c>QueryAsync</c> ordering.</item>
///         <item><c>org_id + action + occurred_at</c> — "show every cluster.create in org X, last 7d".</item>
///     </list>
///     </para>
/// </remarks>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryRecord>
{
    public void Configure(EntityTypeBuilder<AuditEntryRecord> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.AuditEntries);

        builder.HasKey(static entry => entry.Id);

        builder.Property(static entry => entry.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static entry => entry.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        // Actor / Outcome stored as text — queryable as strings, no
        // numeric coupling. HasMaxLength bound the column; the
        // underlying enum names are short (User / Service / Node /
        // System, Succeeded / Failed / Denied).
        builder.Property(static entry => entry.Actor)
            .HasColumnName("actor")
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(static entry => entry.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static entry => entry.Action)
            .HasColumnName("action")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static entry => entry.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(64);

        builder.Property(static entry => entry.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid");

        builder.Property(static entry => entry.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(static entry => entry.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(128);

        builder.Property(static entry => entry.MetadataJson)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(static entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        // Tenant-scoped list — the org-scoped hot path.
        builder.HasIndex(static entry => new { entry.OrgId, entry.OccurredAt })
            .HasDatabaseName("ix_atlas_audit_entries_org_id_occurred_at")
            .IsDescending(false, true);

        // Per-actor timeline — QueryByActorAsync hot path.
        builder.HasIndex(static entry => new { entry.ActorId, entry.OccurredAt })
            .HasDatabaseName("ix_atlas_audit_entries_actor_id_occurred_at")
            .IsDescending(false, true);

        // "All <action> events for this org in the last 7 days"
        // (admin dashboards, security investigations).
        builder.HasIndex(static entry => new { entry.OrgId, entry.Action, entry.OccurredAt })
            .HasDatabaseName("ix_atlas_audit_entries_org_id_action_occurred_at")
            .IsDescending(false, false, true);
    }
}
