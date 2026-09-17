// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// FloatingIpConfiguration — EF Core configuration for the
// network.floating_ips row. snake_case column names, bounded string
// lengths, three indexes that the quota enforcer + admin UI scan:
//   ix_network_floating_ips_org_id           (tenant-scoped count)
//   ix_network_floating_ips_cluster_id       (cluster listing)
//   ix_network_floating_ips_status            (admin UI filter by status)
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Network.Domain.Entities;

namespace Plexor.Modules.Network.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The (org_id) index backs
///     the <c>INetworkQuotaReader.CountAsync</c> org-scoped aggregate.
/// </summary>
internal sealed class FloatingIpConfiguration : IEntityTypeConfiguration<FloatingIp>
{
    /// <summary>IPv6 address cap. The longest legal textual IPv6 is
    /// 39 chars (8 groups × 4 hex digits + 7 colons); 45 leaves one
    /// char of headroom for future IPv6 formats (zone identifiers
    /// like <c>%eth0</c> extend the textual form).</summary>
    private const int AddressMaxLength = 45;

    public void Configure(EntityTypeBuilder<FloatingIp> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.FloatingIps);

        builder.HasKey(static ip => ip.Id);

        builder.Property(static ip => ip.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static ip => ip.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static ip => ip.ClusterId)
            .HasColumnName("cluster_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static ip => ip.Address)
            .HasColumnName("address")
            .HasMaxLength(AddressMaxLength)
            .IsRequired();

        builder.Property(static ip => ip.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static ip => ip.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static ip => ip.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Tenant-scoped aggregate — the quota enforcer's
        // CountAsync runs `COUNT(*) WHERE org_id = ?`.
        builder.HasIndex(static ip => ip.OrgId)
            .HasDatabaseName("ix_network_floating_ips_org_id");

        // Per-cluster listing — the admin UI lists floating IPs by
        // cluster.
        builder.HasIndex(static ip => ip.ClusterId)
            .HasDatabaseName("ix_network_floating_ips_cluster_id");

        // Admin UI filter by lifecycle status.
        builder.HasIndex(static ip => ip.Status)
            .HasDatabaseName("ix_network_floating_ips_status");
    }
}
