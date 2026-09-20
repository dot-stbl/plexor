// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereVirtualMachineConfiguration — EF Core configuration for the
// cached VM row. snake_case + HasMaxLength per .agents/coding/ef-core.md.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + bounded string lengths. The
///     <c>(snapshot_id, moref)</c> unique index guards against
///     duplicate inserts during refresh. The
///     <c>(snapshot_id, folder_path)</c> index backs the UI's
///     "list VMs in a folder" filter — the inventory drilldown by
///     tenant Folder.
/// </summary>
internal sealed class VSphereVirtualMachineConfiguration
    : IEntityTypeConfiguration<VSphereVirtualMachine>
{
    private const int MorefMaxLength = 128;
    private const int NameMaxLength = 256;
    private const int FolderPathMaxLength = 512;
    private const int PowerStateMaxLength = 32;

    public void Configure(EntityTypeBuilder<VSphereVirtualMachine> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.VirtualMachines);

        builder.HasKey(static vm => vm.Id);

        builder.Property(static vm => vm.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static vm => vm.SnapshotId)
            .HasColumnName("snapshot_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static vm => vm.Moref)
            .HasColumnName("moref")
            .HasMaxLength(MorefMaxLength)
            .IsRequired();

        builder.Property(static vm => vm.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(static vm => vm.FolderPath)
            .HasColumnName("folder_path")
            .HasMaxLength(FolderPathMaxLength);

        builder.Property(static vm => vm.PowerState)
            .HasColumnName("power_state")
            .HasMaxLength(PowerStateMaxLength)
            .IsRequired();

        builder.Property(static vm => vm.CpuCount)
            .HasColumnName("cpu_count")
            .IsRequired();

        builder.Property(static vm => vm.MemoryMib)
            .HasColumnName("memory_mib")
            .IsRequired();

        builder.Property(static vm => vm.HostMoref)
            .HasColumnName("host_moref")
            .HasMaxLength(MorefMaxLength);

        builder.HasIndex(static vm => new { vm.SnapshotId, vm.Moref })
            .HasDatabaseName("ux_outpost_vsphere_virtual_machines_snapshot_id_moref")
            .IsUnique();

        builder.HasIndex(static vm => new { vm.SnapshotId, vm.FolderPath })
            .HasDatabaseName("ix_outpost_vsphere_virtual_machines_snapshot_id_folder_path");
    }
}
