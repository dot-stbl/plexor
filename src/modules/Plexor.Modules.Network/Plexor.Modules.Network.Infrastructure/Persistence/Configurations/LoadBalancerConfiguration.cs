// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoadBalancerConfiguration — EF Core configuration for the
// network.load_balancers row. snake_case column names, bounded
// string lengths, three indexes:
//   ix_network_load_balancers_org_id           (tenant-scoped count)
//   ix_network_load_balancers_cluster_id_name  (per-cluster name uniqueness)
//   ix_network_load_balancers_status            (admin UI filter by status)
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Network.Domain.Entities;

namespace Plexor.Modules.Network.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The UNIQUE on
///     <c>(cluster_id, name)</c> matches the per-cluster LB naming
///     invariant; the <c>(org_id)</c> index backs the
///     <c>INetworkQuotaReader.CountAsync</c> org-scoped aggregate.
/// </summary>
internal sealed class LoadBalancerConfiguration : IEntityTypeConfiguration<LoadBalancer>
{
    /// <summary>Load balancer name cap. Matches
    /// <c>Plexor.Modules.Storage.Domain.Entities.Volume.Name</c> —
    /// 128 chars leaves headroom for operator-assigned labels
    /// (<c>prod-edge-lb-001</c>, etc.) without a schema migration.</summary>
    private const int NameMaxLength = 128;

    public void Configure(EntityTypeBuilder<LoadBalancer> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.LoadBalancers);

        builder.HasKey(static lb => lb.Id);

        builder.Property(static lb => lb.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static lb => lb.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static lb => lb.ClusterId)
            .HasColumnName("cluster_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static lb => lb.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        // Type + Algorithm stored as varchar so a future enum
        // member addition does not require a schema migration.
        builder.Property(static lb => lb.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static lb => lb.Algorithm)
            .HasColumnName("algorithm")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(static lb => lb.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static lb => lb.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static lb => lb.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Tenant-scoped aggregate — the quota enforcer's
        // CountAsync runs `COUNT(*) WHERE org_id = ?`.
        builder.HasIndex(static lb => lb.OrgId)
            .HasDatabaseName("ix_network_load_balancers_org_id");

        // Per-cluster name uniqueness.
        builder.HasIndex(static lb => new { lb.ClusterId, lb.Name })
            .HasDatabaseName("ix_network_load_balancers_cluster_id_name")
            .IsUnique();

        // Admin UI filter by lifecycle status.
        builder.HasIndex(static lb => lb.Status)
            .HasDatabaseName("ix_network_load_balancers_status");
    }
}
