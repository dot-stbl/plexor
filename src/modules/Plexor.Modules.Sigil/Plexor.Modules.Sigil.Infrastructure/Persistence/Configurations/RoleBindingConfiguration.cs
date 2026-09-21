// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RoleBindingConfiguration — EF Core configuration for the
// sigil.role_bindings row. snake_case column names, UNIQUE on
// (user_id, role_id, team_id, folder_id) so the same role can't
// be bound twice at the same 3-tier scope, plus secondary
// indexes for "who has what" and role-bound user list queries.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The UNIQUE on
///     <c>(user_id, role_id, team_id, folder_id)</c> enforces the
///     "no duplicate role grant at the same scope" invariant; NULLs
///     in the nullable scope columns are treated as distinct by
///     Postgres, letting org-wide and team-wide bindings coexist.
/// </summary>
internal sealed class RoleBindingConfiguration : IEntityTypeConfiguration<RoleBinding>
{
    public void Configure(EntityTypeBuilder<RoleBinding> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.RoleBindings);

        builder.HasKey(static binding => binding.Id);

        builder.Property(static binding => binding.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static binding => binding.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static binding => binding.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static binding => binding.RoleId)
            .HasColumnName("role_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static binding => binding.TeamId)
            .HasColumnName("team_id")
            .HasColumnType("uuid");

        builder.Property(static binding => binding.FolderId)
            .HasColumnName("folder_id")
            .HasColumnType("uuid");

        builder.Property(static binding => binding.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Can't bind the same role to the same scope twice. The 3-tier
        // scope is (org, team, folder); NULLs in nullable cols are
        // treated as distinct by Postgres unique indexes, so org-wide
        // and team-wide bindings coexist without false collisions.
        builder.HasIndex(static binding => new { binding.UserId, binding.RoleId, binding.TeamId, binding.FolderId })
            .HasDatabaseName("ix_sigil_role_bindings_user_id_role_id_team_id_folder_id")
            .IsUnique();

        builder.HasIndex(static binding => binding.RoleId)
            .HasDatabaseName("ix_sigil_role_bindings_role_id");

        // User-scoped role list queries (who has what).
        builder.HasIndex(static binding => binding.UserId)
            .HasDatabaseName("ix_sigil_role_bindings_user_id");
    }
}