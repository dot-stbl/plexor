// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereClusterConfiguration — EF Core configuration for the cached
// cluster row. snake_case + HasMaxLength per .agents/coding/ef-core.md.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + bounded string lengths. The
///     <c>(snapshot_id, moref)</c> unique index ensures a refresh
///     reload doesn't insert duplicate cluster rows for the same
///     snapshot. The <c>(snapshot_id)</c> + <c>(datacenter_moref)</c>
///     indexes back the UI's per-datacenter filter.
/// </summary>
internal sealed class VSphereClusterConfiguration
    : IEntityTypeConfiguration<VSphereCluster>
{
    /// <summary>Length cap on the vCenter mo-ref column.</summary>
    private const int MorefMaxLength = 128;

    /// <summary>Length cap on the cluster display name.</summary>
    private const int NameMaxLength = 256;

    public void Configure(EntityTypeBuilder<VSphereCluster> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Clusters);

        builder.HasKey(static cluster => cluster.Id);

        builder.Property(static cluster => cluster.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static cluster => cluster.SnapshotId)
            .HasColumnName("snapshot_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static cluster => cluster.Moref)
            .HasColumnName("moref")
            .HasMaxLength(MorefMaxLength)
            .IsRequired();

        builder.Property(static cluster => cluster.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(static cluster => cluster.DatacenterMoref)
            .HasColumnName("datacenter_moref")
            .HasMaxLength(MorefMaxLength)
            .IsRequired();

        builder.Property(static cluster => cluster.DrsEnabled)
            .HasColumnName("drs_enabled")
            .IsRequired();

        builder.HasIndex(static cluster => new { cluster.SnapshotId, cluster.Moref })
            .HasDatabaseName("ux_outpost_vsphere_clusters_snapshot_id_moref")
            .IsUnique();

        builder.HasIndex(static cluster => new { cluster.SnapshotId, cluster.DatacenterMoref })
            .HasDatabaseName("ix_outpost_vsphere_clusters_snapshot_id_datacenter_moref");
    }
}
