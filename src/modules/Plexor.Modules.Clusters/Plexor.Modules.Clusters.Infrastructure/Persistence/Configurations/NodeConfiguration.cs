// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Node EF Core configuration — snake_case columns + HasMaxLength
// per coding/ef-core.md. forge.nodes holds every Plexor.NodeAgent
// that joined a cluster; spec is stored as JSONB and indexes
// cover the dashboard's node-tab read path.
// ============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Configurations;

/// <summary>
///     forge.nodes — joined Plexor.NodeAgent instances. Snake_case
///     columns + HasMaxLength per coding/ef-core.md. Spec is stored
///     as JSONB (Postgres) / JSON string (InMemory); indexes cover
///     the dashboard's node-tab read path.
/// </summary>
internal sealed class NodeConfiguration() : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Nodes);

        builder.HasKey(static node => node.Id);

        builder.Property(static node => node.Id)
            .HasColumnName("id")
            .HasColumnType("varchar(64)")
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseNodeId(raw))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static node => node.ClusterId)
            .HasColumnName("cluster_id")
            .HasColumnType("varchar(64)")
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseClusterId(raw))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static node => node.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static node => node.Hostname)
            .HasColumnName("hostname")
            .HasMaxLength(253)
            .IsRequired();

        builder.Property(static node => node.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(static node => node.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        // NodeSpec — value object. Stored as JSONB (Postgres) / JSON
        // string (InMemory). The converter serializes via System.Text.Json;
        // HasColumnType("jsonb") is only honored by the npgsql provider.
        builder.Property(static node => node.Spec)
            .HasColumnName("spec")
            .HasColumnType("jsonb")
            .HasConversion(
                static spec => JsonSerializer.Serialize(spec, (JsonSerializerOptions?)null),
                static raw => JsonSerializer.Deserialize<NodeSpec>(raw, (JsonSerializerOptions?)null) ?? new(0, 0, 0, Array.Empty<string>()))
            .IsRequired();

        builder.Property(static node => node.IsoVersion)
            .HasColumnName("iso_version")
            .HasMaxLength(32);

        builder.Property(static node => node.LastHeartbeatAt)
            .HasColumnName("last_heartbeat_at");

        builder.Property(static node => node.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static node => node.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(static node => node.WireguardPublicKey)
            .HasColumnName("wireguard_public_key")
            .HasMaxLength(64);

        builder.Property(static node => node.VmCount)
            .HasColumnName("vm_count")
            .HasDefaultValue(0)
            .IsRequired();

        // Hostname is unique per cluster (no two nodes can claim the
        // same OS hostname inside one cluster).
        builder.HasIndex(static node => new { node.ClusterId, node.Hostname })
            .HasDatabaseName("ix_nodes_cluster_id_hostname")
            .IsUnique();

        // Cluster-scoped node list (the dashboard's node tab).
        builder.HasIndex(static node => new { node.ClusterId, node.Status })
            .HasDatabaseName("ix_nodes_cluster_id_status");

        // Explicit FK — Cluster.Nodes is marked Ignore() in the cluster
        // configuration (init-only IReadOnlyList breaks InMemory), so
        // EF can't auto-discover the relationship. Without the explicit
        // declaration there's no FK constraint at the DB level.
        builder.HasOne<Cluster>()
            .WithMany()
            .HasForeignKey(static node => node.ClusterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
