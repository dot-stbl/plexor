// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VolumeConfiguration — EF Core configuration for the storage.volumes
// row. snake_case column names, bounded string lengths, three indexes
// that the quota enforcer + admin UI scan:
//   ix_storage_volumes_org_id           (tenant-scoped count + sum)
//   ix_storage_volumes_cluster_id_name  (per-cluster name uniqueness)
//   ix_storage_volumes_status           (admin UI filter by status)
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Storage.Domain.Entities;

namespace Plexor.Modules.Storage.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The UNIQUE on
///     <c>(cluster_id, name)</c> matches the per-cluster volume naming
///     invariant; the <c>(org_id)</c> index backs the
///     <c>IStorageQuotaReader.CountAsync</c> org-scoped aggregate.
/// </summary>
internal sealed class VolumeConfiguration : IEntityTypeConfiguration<Volume>
{
    /// <summary>Volume name cap. 128 chars leaves headroom for
    /// operator-assigned labels (<c>prod-data-001</c>, etc.) without
    /// a schema migration.</summary>
    private const int NameMaxLength = 128;

    public void Configure(EntityTypeBuilder<Volume> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Volumes);

        builder.HasKey(static volume => volume.Id);

        builder.Property(static volume => volume.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        // Tenant scope. Denormalized so the quota enforcer's
        // CountAsync can scope the COUNT(*) without joining Realm.
        builder.Property(static volume => volume.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static volume => volume.ClusterId)
            .HasColumnName("cluster_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static volume => volume.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(static volume => volume.SizeGb)
            .HasColumnName("size_gb")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(static volume => volume.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static volume => volume.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static volume => volume.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Tenant-scoped aggregate — the quota enforcer's
        // CountAsync runs `COUNT(*) + SUM(size_gb) WHERE org_id = ?`.
        builder.HasIndex(static volume => volume.OrgId)
            .HasDatabaseName("ix_storage_volumes_org_id");

        // Per-cluster name uniqueness — volumes are scoped by the
        // cluster the disk belongs to (a node hosts N volumes).
        builder.HasIndex(static volume => new { volume.ClusterId, volume.Name })
            .HasDatabaseName("ix_storage_volumes_cluster_id_name")
            .IsUnique();

        // Admin UI filter by lifecycle status (Pending / Attached /
        // Error triage views).
        builder.HasIndex(static volume => volume.Status)
            .HasDatabaseName("ix_storage_volumes_status");
    }
}
