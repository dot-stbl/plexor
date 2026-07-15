// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterDbContext — EF Core context for the Clusters module. Persists
// clusters, nodes, and join_tokens in the 'forge' PostgreSQL schema
// (schema-per-module per .agents/STATE.md). All column names + the
// schema constant live in Plexor.Shared.Persistence.DatabaseInformation.
// ============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

#pragma warning disable CS1591 // XML doc — see DbSet<T> props for usage

public sealed class ClusterDbContext(DbContextOptions<ClusterDbContext> options) : PlexorDbContext(options)
{
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<JoinToken> JoinTokens => Set<JoinToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Clusters)
            .ApplyConfiguration(new ClusterConfiguration())
            .ApplyConfiguration(new NodeConfiguration())
            .ApplyConfiguration(new JoinTokenConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
///     Snake_case column names + HasMaxLength per coding/ef-core.md. Indexes
///     cover the read paths: org-scoped cluster list, unique name per org,
///     status-filtered lists for the dashboard.
/// </summary>
internal sealed class ClusterConfiguration : IEntityTypeConfiguration<Cluster>
{
    public void Configure(EntityTypeBuilder<Cluster> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Clusters);

        builder.HasKey(static cluster => cluster.Id);

        // Navigation properties loaded via separate queries (GetClusterQueryHandler).
        // Not mapped — the init-only IReadOnlyList<Node> default breaks EF's
        // collection change tracker on InMemory.
        builder.Ignore(static cluster => cluster.Nodes);
        builder.Ignore(static cluster => cluster.Tokens);

        builder.Property(static cluster => cluster.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static cluster => cluster.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static cluster => cluster.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static cluster => cluster.Region)
            .HasColumnName("region")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static cluster => cluster.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(static cluster => cluster.WireguardPublicKey)
            .HasColumnName("wireguard_public_key")
            .HasMaxLength(64);

        builder.Property(static cluster => cluster.JoinTokenExpiresAt)
            .HasColumnName("join_token_expires_at");

        // InstallProviders: IReadOnlyList<string> ↔ JSON string.
        // JSON (not text[]) so the converter works uniformly across
        // npgsql + InMemory — composing IReadOnlyList<string> → string[]
        // with the provider's own converter breaks on non-relational.
        // Postgres stores it as jsonb; InMemory stores the raw string.
        builder.Property(static cluster => cluster.InstallProviders)
            .HasColumnName("install_providers")
            .HasColumnType("jsonb")
            .HasConversion(
                static providers => JsonSerializer.Serialize(providers, (JsonSerializerOptions?)null),
                static raw => JsonSerializer.Deserialize<List<string>>(raw, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<string>>(
                static (a, b) => (a == null && b == null) ||
                    (a != null && b != null && a.SequenceEqual(b)),
                static v => v.Aggregate(0, static (acc, s) => HashCode.Combine(acc, s.GetHashCode(StringComparison.Ordinal))),
                static v => (IReadOnlyList<string>)new List<string>(v)));

        builder.Property(static cluster => cluster.HostVersion)
            .HasColumnName("host_version")
            .HasMaxLength(32);

        builder.Property(static cluster => cluster.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(256);

        builder.Property(static cluster => cluster.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static cluster => cluster.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(static cluster => cluster.Uptime)
            .HasColumnName("uptime")
            .HasConversion(
                static span => span.Ticks,
                static ticks => TimeSpan.FromTicks(ticks));

        // Name is unique per organization (enforced here; cross-org
        // cluster-name reuse is fine).
        builder.HasIndex(static cluster => new { cluster.OrgId, cluster.Name })
            .HasDatabaseName("ix_clusters_org_id_name")
            .IsUnique();

        // Org-scoped cluster list queries (dashboard, admin endpoints).
        builder.HasIndex(static cluster => new { cluster.OrgId, cluster.Status })
            .HasDatabaseName("ix_clusters_org_id_status");
    }
}

internal sealed class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Nodes);

        builder.HasKey(static node => node.Id);

        builder.Property(static node => node.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static node => node.ClusterId)
            .HasColumnName("cluster_id")
            .HasColumnType("uuid")
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

internal sealed class JoinTokenConfiguration : IEntityTypeConfiguration<JoinToken>
{
    public void Configure(EntityTypeBuilder<JoinToken> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.JoinTokens);

        builder.HasKey(static token => token.Id);

        builder.Property(static token => token.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static token => token.ClusterId)
            .HasColumnName("cluster_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static token => token.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static token => token.Label)
            .HasColumnName("label")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static token => token.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(static token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static token => token.IntendedRole)
            .HasColumnName("intended_role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(static token => token.MinIsoVersion)
            .HasColumnName("min_iso_version")
            .HasMaxLength(32);

        builder.Property(static token => token.IssuedAt)
            .HasColumnName("issued_at")
            .IsRequired();

        builder.Property(static token => token.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(static token => token.RedeemedByNodeId)
            .HasColumnName("redeemed_by_node_id")
            .HasColumnType("uuid");

        // Lookup the active token for a cluster (rotate-then-revoke flow).
        builder.HasIndex(static token => new { token.ClusterId, token.Status })
            .HasDatabaseName("ix_join_tokens_cluster_id_status");

        // TTL sweeper — find tokens past expiry.
        builder.HasIndex(static token => token.ExpiresAt)
            .HasDatabaseName("ix_join_tokens_expires_at");

        // Explicit FK — Cluster.Tokens is marked Ignore() in the cluster
        // configuration (init-only IReadOnlyList breaks InMemory), so
        // EF can't auto-discover the relationship.
        builder.HasOne<Cluster>()
            .WithMany()
            .HasForeignKey(static token => token.ClusterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
