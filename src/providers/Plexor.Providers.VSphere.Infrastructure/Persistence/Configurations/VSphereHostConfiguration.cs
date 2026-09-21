// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereHostConfiguration — EF Core configuration for the cached
// host row. snake_case + HasMaxLength per .agents/coding/ef-core.md.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + bounded string lengths. The
///     <c>(snapshot_id, cluster_moref)</c> index backs the UI's
///     "list hosts in a cluster" filter — the most common inventory
///     drilldown. The <c>(snapshot_id, moref)</c> unique index
///     guards against duplicate inserts during refresh.
/// </summary>
internal sealed class VSphereHostConfiguration
    : IEntityTypeConfiguration<VSphereHost>
{
    private const int MorefMaxLength = 128;
    private const int NameMaxLength = 256;
    private const int ConnectionStateMaxLength = 32;

    public void Configure(EntityTypeBuilder<VSphereHost> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Hosts);

        builder.HasKey(static host => host.Id);

        builder.Property(static host => host.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static host => host.SnapshotId)
            .HasColumnName("snapshot_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static host => host.Moref)
            .HasColumnName("moref")
            .HasMaxLength(MorefMaxLength)
            .IsRequired();

        builder.Property(static host => host.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(static host => host.ClusterMoref)
            .HasColumnName("cluster_moref")
            .HasMaxLength(MorefMaxLength)
            .IsRequired();

        builder.Property(static host => host.ConnectionState)
            .HasColumnName("connection_state")
            .HasMaxLength(ConnectionStateMaxLength)
            .IsRequired();

        builder.Property(static host => host.CpuCores)
            .HasColumnName("cpu_cores")
            .IsRequired();

        builder.Property(static host => host.MemoryMib)
            .HasColumnName("memory_mib")
            .IsRequired();

        builder.HasIndex(static host => new { host.SnapshotId, host.Moref })
            .HasDatabaseName("ux_outpost_vsphere_hosts_snapshot_id_moref")
            .IsUnique();

        builder.HasIndex(static host => new { host.SnapshotId, host.ClusterMoref })
            .HasDatabaseName("ix_outpost_vsphere_hosts_snapshot_id_cluster_moref");
    }
}
