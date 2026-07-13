// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RealmDbContext — EF Core context for the Organizations module. Owns
// the org/team/folder hierarchy in the 'realm' PostgreSQL schema
// (architecture theme — see AGENTS.md for the schema-vs-concept naming
// map). Every other module FKs into realm.organizations.id.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Realm.Infrastructure.Persistence;

#pragma warning disable CS1591 // XML doc — see DbContext.Set<T>() for usage

public sealed class RealmDbContext(DbContextOptions<RealmDbContext> options) : PlexorDbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Folder> Folders => Set<Folder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Realm)
            .ApplyConfiguration(new OrganizationConfiguration())
            .ApplyConfiguration(new TeamConfiguration())
            .ApplyConfiguration(new FolderConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Organizations);

        builder.HasKey(static org => org.Id);

        builder.Property(static org => org.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static org => org.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static org => org.Slug)
            .HasColumnName("slug")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static org => org.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static org => org.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Slug is globally unique — used in login URL + cross-tenant
        // resolution before the password check.
        builder.HasIndex(static org => org.Slug)
            .HasDatabaseName("ix_realm_organizations_slug")
            .IsUnique();
    }
}

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Teams);

        builder.HasKey(static team => team.Id);

        builder.Property(static team => team.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static team => team.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static team => team.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static team => team.Slug)
            .HasColumnName("slug")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static team => team.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static team => team.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Slug is unique per org (two teams in different orgs may share
        // a slug; same org cannot).
        builder.HasIndex(static team => new { team.OrgId, team.Slug })
            .HasDatabaseName("ix_realm_teams_org_id_slug")
            .IsUnique();

        // Org-scoped team list queries.
        builder.HasIndex(static team => team.OrgId)
            .HasDatabaseName("ix_realm_teams_org_id");
    }
}

internal sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Folders);

        builder.HasKey(static folder => folder.Id);

        builder.Property(static folder => folder.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static folder => folder.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static folder => folder.TeamId)
            .HasColumnName("team_id")
            .HasColumnType("uuid");

        builder.Property(static folder => folder.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(static folder => folder.Slug)
            .HasColumnName("slug")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static folder => folder.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static folder => folder.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Slug is unique per (org, team). For org-level folders
        // (team_id = null) Postgres treats the NULL rows as distinct,
        // so we add a separate partial unique index for the
        // org-level case via HasFilter.
        builder.HasIndex(static folder => new { folder.OrgId, folder.TeamId, folder.Slug })
            .HasDatabaseName("ix_realm_folders_org_id_team_id_slug")
            .IsUnique();

        // Org-scoped folder list queries.
        builder.HasIndex(static folder => folder.OrgId)
            .HasDatabaseName("ix_realm_folders_org_id");

        // Team-scoped folder list queries.
        builder.HasIndex(static folder => folder.TeamId)
            .HasDatabaseName("ix_realm_folders_team_id");
    }
}
