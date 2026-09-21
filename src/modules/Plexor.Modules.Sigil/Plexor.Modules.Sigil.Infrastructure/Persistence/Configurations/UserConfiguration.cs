// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UserConfiguration — EF Core configuration for the sigil.users row.
// snake_case column names, bounded string lengths, value-object
// converters (Email, PasswordHash). Two indexes cover the main read
// paths: login lookup (org_id + email) and admin list
// (org_id + status).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;

/// <summary>
///     Snake_case column names + HasMaxLength per coding/ef-core.md. Email
///     is stored as <c>varchar(320)</c> (RFC 5321 max). Indexes cover
///     the main read paths: login lookup (org_id + email),
///     refresh-token validation (family_id), API-key validation
///     (revoked_at IS NULL), org-scoped role/user queries.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Users);

        builder.HasKey(static user => user.Id);

        builder.Property(static user => user.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static user => user.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .HasConversion(
                static email => email.Value,
                static raw => new Email(raw))
            .IsRequired();

        builder.Property(static user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static user => user.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired();

        // PasswordHash: nullable in DB (OAuth-only users have null). The
        // value-object enforces the bcrypt format on construction; the
        // converter stores the underlying string and re-parses on read.
        builder.Property(static user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .HasConversion(
                static hash => hash == null ? null : hash.ToString(),
                static raw => raw == null ? null : new PasswordHash(raw));

        builder.Property(static user => user.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(static user => user.LockedUntil)
            .HasColumnName("locked_until");

        builder.Property(static user => user.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(static user => user.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static user => user.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(static user => user.PasswordChangedAt)
            .HasColumnName("password_changed_at");

        // Email is unique per organization (cross-org users are Phase 2).
        builder.HasIndex(static user => new { user.OrgId, user.Email })
            .HasDatabaseName("ix_sigil_users_org_id_email")
            .IsUnique();

        // Org-scoped user list queries (admin endpoints).
        builder.HasIndex(static user => new { user.OrgId, user.Status })
            .HasDatabaseName("ix_sigil_users_org_id_status");
    }
}