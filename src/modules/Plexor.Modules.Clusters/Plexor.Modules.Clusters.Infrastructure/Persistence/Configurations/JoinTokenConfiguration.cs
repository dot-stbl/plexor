// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JoinToken EF Core configuration — snake_case columns + HasMaxLength
// per coding/ef-core.md. forge.join_tokens holds one-time credentials
// for first node attach; indexes support the rotate-then-revoke flow
// and the TTL sweeper.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Configurations;

/// <summary>
///     forge.join_tokens — one-time credentials for first node attach.
///     Snake_case columns + HasMaxLength per coding/ef-core.md. Indexes
///     support the rotate-then-revoke flow and the TTL sweeper.
/// </summary>
internal sealed class JoinTokenConfiguration() : IEntityTypeConfiguration<JoinToken>
{
    public void Configure(EntityTypeBuilder<JoinToken> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.JoinTokens);

        builder.HasKey(static token => token.Id);

        builder.Property(static token => token.Id)
            .HasColumnName("id")
            .HasColumnType("varchar(64)")
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseTokenId(raw))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static token => token.ClusterId)
            .HasColumnName("cluster_id")
            .HasColumnType("varchar(64)")
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseClusterId(raw))
            .HasMaxLength(64)
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
            .HasColumnType("varchar(64)")
            .HasConversion<NullableNodeIdConverter>();

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
