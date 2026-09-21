// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RoleConfiguration — EF Core configuration for the sigil.roles row.
// snake_case column names, Permissions mapped to a text[] column via
// a value-comparer that compares element-wise, and a UNIQUE on
// (org_id, name) to prevent duplicate role definitions per tenant.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. Permissions are stored as
///     a <c>text[]</c> column with an element-wise value comparer
///     so the change tracker treats a re-ordered permission list
///     as a no-op.
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Roles);

        builder.HasKey(static role => role.Id);

        builder.Property(static role => role.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static role => role.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static role => role.Name)
            .HasColumnName("name")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static role => role.Description)
            .HasColumnName("description");

        // Permissions: IReadOnlyList<PermissionScope> ↔ string[] (text[] column).
        // The PermissionScope type is rich in the domain layer (format
        // validation, equality, hash code); the database stores the
        // lowercased Value strings.
        builder.Property(static role => role.Permissions)
            .HasColumnName("permissions")
            .HasColumnType("text[]")
            .HasConversion(
                static permissions => permissions.Select(static p => p.Value).ToArray(),
                static raw => raw.Select(
                    static value => new PermissionScope(value)).ToArray())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<PermissionScope>>(
                static (a, b) => (a == null && b == null) ||
                    (a != null && b != null && a.SequenceEqual(b)),
                static v => v.Aggregate(0, static (acc, p) => HashCode.Combine(acc, p.GetHashCode())),
                static v => v.Select(
                    static p => new PermissionScope(p.Value)).ToArray()));

        builder.Property(static role => role.BuiltIn)
            .HasColumnName("built_in")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(static role => role.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static role => role.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(static role => new { role.OrgId, role.Name })
            .HasDatabaseName("ix_sigil_roles_org_id_name")
            .IsUnique();
    }
}