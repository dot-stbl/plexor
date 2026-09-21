// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshTokenConfiguration — EF Core configuration for the
// sigil.refresh_tokens row. snake_case column names, three indexes
// that the auth pipeline scans:
//   ix_sigil_refresh_tokens_user_id     (per-user revocation sweep)
//   ix_sigil_refresh_tokens_family_id   (family-level revocation)
//   ix_sigil_refresh_tokens_expires_at  (retention sweeper)
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The token_hash is a hex
///     SHA-256, fixed at 64 chars; the family_id backs the
///     "revoke the entire family on reuse-detect" auth invariant.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.RefreshTokens);

        builder.HasKey(static token => token.Id);

        builder.Property(static token => token.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static token => token.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static token => token.FamilyId)
            .HasColumnName("family_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(static token => token.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(static token => token.ReplacedBy)
            .HasColumnName("replaced_by")
            .HasColumnType("uuid");

        builder.Property(static token => token.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(static token => token.UserId)
            .HasDatabaseName("ix_sigil_refresh_tokens_user_id");

        builder.HasIndex(static token => token.FamilyId)
            .HasDatabaseName("ix_sigil_refresh_tokens_family_id");

        builder.HasIndex(static token => token.ExpiresAt)
            .HasDatabaseName("ix_sigil_refresh_tokens_expires_at");
    }
}