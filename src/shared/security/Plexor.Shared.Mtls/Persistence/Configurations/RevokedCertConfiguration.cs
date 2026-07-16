// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RevokedCertConfiguration — EF Core fluent mapping for the
// forge.revoked_certs table. Kept in its own file (not inlined in
// the DbContext) so the entity and the schema configuration can be
// reviewed independently — schema drift is the most common source
// of migration bugs.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Shared.Mtls.Entities;

namespace Plexor.Shared.Mtls.Persistence.Configurations;

/// <summary>
///     Fluent mapping for <see cref="RevokedCert" />. Schema name +
///     column names + indexes. Snake-case per .agents/rules/coding/
///     ef-core.md; the schema ("forge") is shared with the
///     <c>Plexor.Modules.Clusters</c> tables so the revoke cascade
///     on cluster delete can insert from the same transaction.
/// </summary>
internal sealed class RevokedCertConfiguration : IEntityTypeConfiguration<RevokedCert>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RevokedCert> builder)
    {
        builder.ToTable("revoked_certs");

        builder.HasKey(static r => r.Serial);

        builder.Property(static r => r.Serial)
            .HasColumnName("serial")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static r => r.RevokedAt)
            .HasColumnName("revoked_at")
            .IsRequired();

        builder.Property(static r => r.RevokedBy)
            .HasColumnName("revoked_by")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static r => r.Reason)
            .HasColumnName("reason")
            .HasMaxLength(256);

        builder.HasIndex(static r => r.RevokedAt)
            .HasDatabaseName("ix_revoked_certs_revoked_at");
    }
}
