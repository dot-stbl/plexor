// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditEntryConfiguration — EF Core configuration for the audit_entries
// row. snake_case column names, bounded string lengths, and the two
// indexes the admin UI + retention sweeper will scan:
//   ix_atlas_audit_entries_org_id_occurred_at (org-scoped timeline)
//   ix_atlas_audit_entries_action_occurred_at (action-scoped scan)
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Audit.Domain.Entities;

namespace Plexor.Modules.Audit.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The two indexes back the
///     standard admin queries ("list events for org X between T1
///     and T2") and the future retention sweep ("delete rows older
///     than N days for action X").
/// </summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <summary>Wire-name length cap. The longest dot.case path in
    /// <see cref="Plexor.Shared.Kernel.Audit.AuditActions" /> is
    /// <c>"quotas.limit.approaching"</c> (24 chars); 64 leaves room
    /// for <c>"org.auth_provider.changed"</c> + future additions
    /// without a schema migration.</summary>
    private const int ActionMaxLength = 64;

    /// <summary>TargetKind discriminator length. The longest value
    /// the quota delegation emits today is <c>"quota_assignment"</c>
    /// (16 chars); 32 covers <c>"org_auth_provider_config"</c> + any
    /// single-word extension without a migration.</summary>
    private const int TargetKindMaxLength = 32;

    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.AuditEntries);

        builder.HasKey(static entry => entry.Id);

        builder.Property(static entry => entry.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static entry => entry.Action)
            .HasColumnName("action")
            .HasMaxLength(ActionMaxLength)
            .IsRequired();

        builder.Property(static entry => entry.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static entry => entry.ActorUserId)
            .HasColumnName("actor_user_id")
            .HasColumnType("uuid");

        builder.Property(static entry => entry.TargetKind)
            .HasColumnName("target_kind")
            .HasMaxLength(TargetKindMaxLength)
            .IsRequired();

        builder.Property(static entry => entry.TargetId)
            .HasColumnName("target_id")
            .HasColumnType("uuid");

        builder.Property(static entry => entry.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(static entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(static entry => entry.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Tenant-scoped timeline — every admin endpoint reads
        // "events for org X between T1 and T2". Index is on
        // (org_id, occurred_at DESC) so the admin list query is a
        // single index range scan.
        builder.HasIndex(static entry => new { entry.OrgId, entry.OccurredAt })
            .HasDatabaseName("ix_atlas_audit_entries_org_id_occurred_at")
            .IsDescending(false, true);

        // Action-scoped timeline — the future retention sweep reads
        // "rows older than N days for action X" and operators look up
        // "every auth-provider change ever". Same shape, action-
        // first column.
        builder.HasIndex(static entry => new { entry.Action, entry.OccurredAt })
            .HasDatabaseName("ix_atlas_audit_entries_action_occurred_at")
            .IsDescending(false, true);
    }
}
