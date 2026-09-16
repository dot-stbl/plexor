// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRecordConfiguration — EF Core configuration for outpost.node_records.
//
// snake_case column names + HasMaxLength per coding/ef-core.md.
// Indexes cover the four read paths:
//   ix_outpost_node_records_cluster_id_hostname (UNIQUE — no two nodes
//     in the same cluster can claim the same OS hostname)
//   ix_outpost_node_records_cluster_id_status (cluster-scoped dashboard list)
//   ix_outpost_node_records_org_id_status (org-scoped admin list)
//   ix_outpost_node_records_last_heartbeat_at (heartbeat evaluator + ops scan)
//
// jsonb is used for the spec column (provider list is a closed set,
// but the wire shape evolves independently of the schema).
// ============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Outpost.Application;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Outpost.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per coding/ef-core.md.
///     Indexes cover the four read paths documented on the class.
/// </summary>
internal sealed class NodeRecordConfiguration : IEntityTypeConfiguration<NodeRecord>
{
    /// <summary>
    ///     Hostname upper bound. RFC 1123 hostname length cap (253 octets
    ///     for the fully-qualified form). 256 leaves room for future
    ///     variations without a schema migration.
    /// </summary>
    private const int HostnameMaxLength = 256;

    /// <summary>IPv4/IPv6 textual length cap. Both fit well under 64.</summary>
    private const int IpAddressMaxLength = 64;

    /// <summary>WireGuard public key base64 length (32-byte key → 44 chars).</summary>
    private const int WireguardPublicKeyMaxLength = 64;

    /// <summary>ISO image version label length cap.</summary>
    private const int IsoVersionMaxLength = 32;

    public void Configure(EntityTypeBuilder<NodeRecord> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.NodeRecords);

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
            .HasMaxLength(HostnameMaxLength)
            .IsRequired();

        builder.Property(static node => node.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(IpAddressMaxLength)
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
                static raw => JsonSerializer.Deserialize<NodeSpec>(raw, (JsonSerializerOptions?)null)
                ?? new NodeSpec(0, 0, 0, Array.Empty<string>()))
            .IsRequired();

        builder.Property(static node => node.IsoVersion)
            .HasColumnName("iso_version")
            .HasMaxLength(IsoVersionMaxLength);

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
            .HasMaxLength(WireguardPublicKeyMaxLength);

        builder.Property(static node => node.VmCount)
            .HasColumnName("vm_count")
            .HasDefaultValue(0)
            .IsRequired();

        // Hostname is unique per cluster — no two nodes can claim
        // the same OS hostname inside one cluster.
        builder.HasIndex(static node => new { node.ClusterId, node.Hostname })
            .HasDatabaseName("ix_outpost_node_records_cluster_id_hostname")
            .IsUnique();

        // Cluster-scoped node list (the dashboard's node tab).
        builder.HasIndex(static node => new { node.ClusterId, node.Status })
            .HasDatabaseName("ix_outpost_node_records_cluster_id_status");

        // Org-scoped admin list.
        builder.HasIndex(static node => new { node.OrgId, node.Status })
            .HasDatabaseName("ix_outpost_node_records_org_id_status");

        // Heartbeat evaluator + ops scan — find nodes whose last
        // heartbeat is older than the staleness threshold.
        builder.HasIndex(static node => node.LastHeartbeatAt)
            .HasDatabaseName("ix_outpost_node_records_last_heartbeat_at");

        // Soft FK to forge.clusters — no navigation property (we
        // never query Outpost→Clusters across DbContexts; the
        // relationship is enforced at the application layer via the
        // JoinToken lookup in RegisterAsync / HeartbeatAsync).
    }
}