// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigningKeyConfiguration — EF Core configuration for the
// sigil.signing_keys row. snake_case column names, kid is the
// primary key (RFC 7517 — kid is a short opaque identifier),
// private_key_pem is nullable so the row records a public-only
// mirror after the matching private key has been destroyed.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The kid is the JWT
///     key id, not a UUID — string PK keeps the column human
///     copy-paste friendly for ops rotation flows.
/// </summary>
internal sealed class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKey>
{
    public void Configure(EntityTypeBuilder<SigningKey> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.SigningKeys);

        builder.HasKey(static key => key.Kid);

        builder.Property(static key => key.Kid)
            .HasColumnName("kid")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(static key => key.Algorithm)
            .HasColumnName("algorithm")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static key => key.PublicKeyPem)
            .HasColumnName("public_key_pem")
            .IsRequired();

        builder.Property(static key => key.PrivateKeyPem)
            .HasColumnName("private_key_pem");

        builder.Property(static key => key.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static key => key.NotAfter)
            .HasColumnName("not_after");
    }
}