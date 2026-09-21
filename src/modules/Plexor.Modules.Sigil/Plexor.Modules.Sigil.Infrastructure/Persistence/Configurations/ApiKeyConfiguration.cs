// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ApiKeyConfiguration — EF Core configuration for the sigil.api_keys
// row. snake_case column names, Permissions mapped to a text[] column
// via the shared PermissionScopeListValueConverter, and two indexes:
//   ix_sigil_api_keys_org_id_revoked_at  (active keys per tenant)
//   ix_sigil_api_keys_user_id            (per-user revocation sweep)
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.ValueConverters;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. Permissions are stored as
///     a <c>text[]</c> column; the <c>revoked_at IS NULL</c> filter
///     is applied at query time, not in the index itself.
/// </summary>
internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.ApiKeys);

        builder.HasKey(static key => key.Id);

        builder.Property(static key => key.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static key => key.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static key => key.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static key => key.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static key => key.SecretHash)
            .HasColumnName("secret_hash")
            .HasMaxLength(64)
            .IsRequired();

        // Permissions: IReadOnlyList<PermissionScope> ↔ string[] (text[] column).
        builder.Property(static key => key.Permissions)
            .HasColumnName("permissions")
            .HasColumnType("text[]")
            .HasConversion<PermissionScopeListValueConverter>()
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<PermissionScope>>(
                static (a, b) => (a == null && b == null) ||
                    (a != null && b != null && a.SequenceEqual(b)),
                static v => v.Aggregate(0, static (acc, p) => HashCode.Combine(acc, p.GetHashCode())),
                static v => v.Select(
                    static p => new PermissionScope(p.Value)).ToArray()));

        builder.Property(static key => key.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(static key => key.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(static key => key.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(static key => key.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Active API keys lookup by tenant (revoked_at IS NULL filter is
        // applied at query time, not in the index itself).
        builder.HasIndex(static key => new { key.OrgId, key.RevokedAt })
            .HasDatabaseName("ix_sigil_api_keys_org_id_revoked_at");

        builder.HasIndex(static key => key.UserId)
            .HasDatabaseName("ix_sigil_api_keys_user_id");
    }
}