// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SshKeyConfiguration — EF Core configuration for the sigil.ssh_keys
// row. snake_case column names, public_key stored as unbounded text
// (RFC 4253 keys are operator-supplied, no fixed cap), and a UNIQUE
// on (org_id, fingerprint) so the same key can't be registered twice
// in the same tenant.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. Fingerprint is a hex
///     SHA-256 (fixed 64 chars); the public_key column is unbounded
///     because operators may register RSA-4096, Ed25519, or future
///     formats — wire-length drives the only real cap.
/// </summary>
internal sealed class SshKeyConfiguration : IEntityTypeConfiguration<SshKey>
{
    public void Configure(EntityTypeBuilder<SshKey> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.SshKeys);

        builder.HasKey(static key => key.Id);

        builder.Property(static key => key.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static key => key.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static key => key.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static key => key.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static key => key.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static key => key.PublicKey)
            .HasColumnName("public_key")
            .IsRequired();

        builder.Property(static key => key.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(static key => key.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(static key => key.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Fingerprint is globally unique per RFC 4253 (modulo tenant
        // scoping for the FK + audit-trail reasons).
        builder.HasIndex(static key => new { key.OrgId, key.Fingerprint })
            .HasDatabaseName("ix_sigil_ssh_keys_org_id_fingerprint")
            .IsUnique();
    }
}