// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventorySnapshotConfiguration — EF Core configuration for the
// snapshot header row. snake_case column names, bounded string
// lengths, and the index the admin UI / future retention sweeper
// will scan.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The
///     <c>(refreshed_at desc)</c> index backs the admin UI's
///     "show the most recent snapshot" query.
/// </summary>
internal sealed class VSphereInventorySnapshotConfiguration
    : IEntityTypeConfiguration<VSphereInventorySnapshot>
{
    /// <summary>Length cap on the vCenter mo-ref column. The
    /// longest realistic mo-ref today is well under 64 chars;
    /// 128 leaves room for future identifier expansion without
    /// a migration.</summary>
    private const int MorefMaxLength = 128;

    public void Configure(EntityTypeBuilder<VSphereInventorySnapshot> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.InventorySnapshots);

        builder.HasKey(static snapshot => snapshot.Id);

        builder.Property(static snapshot => snapshot.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static snapshot => snapshot.VCenterMoref)
            .HasColumnName("vcenter_moref")
            .HasMaxLength(MorefMaxLength)
            .IsRequired();

        builder.Property(static snapshot => snapshot.DatacenterCount)
            .HasColumnName("datacenter_count")
            .IsRequired();

        builder.Property(static snapshot => snapshot.ClusterCount)
            .HasColumnName("cluster_count")
            .IsRequired();

        builder.Property(static snapshot => snapshot.HostCount)
            .HasColumnName("host_count")
            .IsRequired();

        builder.Property(static snapshot => snapshot.VirtualMachineCount)
            .HasColumnName("virtual_machine_count")
            .IsRequired();

        builder.Property(static snapshot => snapshot.RefreshedAt)
            .HasColumnName("refreshed_at")
            .IsRequired();

        builder.Property(static snapshot => snapshot.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasMany(static snapshot => snapshot.Clusters)
            .WithOne(static cluster => cluster.Snapshot)
            .HasForeignKey(static cluster => cluster.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(static snapshot => snapshot.Hosts)
            .WithOne(static host => host.Snapshot)
            .HasForeignKey(static host => host.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(static snapshot => snapshot.VirtualMachines)
            .WithOne(static vm => vm.Snapshot)
            .HasForeignKey(static vm => vm.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(static snapshot => snapshot.RefreshedAt)
            .HasDatabaseName("ix_outpost_vsphere_inventory_snapshots_refreshed_at")
            .IsDescending(true);
    }
}
