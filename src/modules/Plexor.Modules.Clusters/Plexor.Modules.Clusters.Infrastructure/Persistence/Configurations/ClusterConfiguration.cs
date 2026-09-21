// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Cluster EF Core configuration — snake_case columns + HasMaxLength
// per coding/ef-core.md. Indexes cover the read paths: org-scoped
// cluster list, unique name per org, status-filtered lists for the
// dashboard.
// ============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Configurations;

/// <summary>
///     forge.clusters — Plexor.Host + joined nodes, one row per fleet.
///     Snake_case columns + HasMaxLength per coding/ef-core.md. Indexes
///     cover the read paths: org-scoped cluster list, unique name per
///     org, status-filtered lists for the dashboard.
/// </summary>
internal sealed class ClusterConfiguration() : IEntityTypeConfiguration<Cluster>
{
    public void Configure(EntityTypeBuilder<Cluster> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Clusters);

        builder.HasKey(static cluster => cluster.Id);

        // Navigation properties loaded via separate queries (GetClusterQueryHandler).
        // Not mapped — the init-only IReadOnlyList<Node> default breaks EF's
        // collection change tracker on InMemory.
        builder.Ignore(static cluster => cluster.Nodes)
            .Ignore(static cluster => cluster.Tokens);

        builder.Property(static cluster => cluster.Id)
            .HasColumnName("id")
            .HasColumnType("varchar(64)")
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseClusterId(raw))
            .HasMaxLength(64)
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
                static v => new List<string>(v)));

        builder.Property(static cluster => cluster.HostVersion)
            .HasColumnName("host_version")
            .HasMaxLength(32);

        builder.Property(static cluster => cluster.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(256);

        // RuntimeId — closed set of supported runtimes (docker-compose |
        // podman-quadlet | k3s). Validated at command-handler level;
        // the column accepts any string but the handler rejects
        // unknown values before they ever land in the row.
        builder.Property(static cluster => cluster.RuntimeId)
            .HasColumnName("runtime_id")
            .HasMaxLength(64)
            .HasDefaultValue(ClusterRuntimeIds.Default)
            .IsRequired();

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
