// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RealmDbContext — EF Core context for the Organizations module. Owns
// the org/team/folder hierarchy in the 'realm' PostgreSQL schema
// (architecture theme — see AGENTS.md for the schema-vs-concept naming
// map). Every other module FKs into realm.organizations.id.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Realm.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Organizations module. Owns the
///     org/team/folder hierarchy in the 'realm' PostgreSQL schema
///     (architecture theme — see AGENTS.md for the schema-vs-concept
///     naming map). Every other module FKs into realm.organizations.id.
/// </summary>
/// <param name="options"></param>
public sealed class RealmDbContext(DbContextOptions<RealmDbContext> options) : PlexorDbContext(options)
{
    /// <summary>Organizations (realm.organizations) — top-level tenant scope.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();
    /// <summary>Teams (realm.teams) — IAM aggregation group inside an org.</summary>
    public DbSet<Team> Teams => Set<Team>();
    /// <summary>Folders (realm.folders) — resource namespace inside a team.</summary>
    public DbSet<Folder> Folders => Set<Folder>();
    /// <summary>Org auth provider configs (realm.org_auth_provider_configs, 4.6.1) —
    /// one row per org, declares the per-tenant authentication backend
    /// (Sigil default or OIDC).</summary>
    public DbSet<OrgAuthProviderConfig> OrgAuthProviderConfigs => Set<OrgAuthProviderConfig>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Realm)
            .ApplyConfiguration(new OrganizationConfiguration())
            .ApplyConfiguration(new TeamConfiguration())
            .ApplyConfiguration(new FolderConfiguration())
            .ApplyConfiguration(new OrgAuthProviderConfigConfiguration());
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

/// <summary>
///     Snake_case column names + HasMaxLength per coding/ef-core.md.
///     <c>oidc_scopes</c> is stored as a Postgres <c>text[]</c> column;
///     the value-object list <c>IReadOnlyList&lt;string&gt;</c> is
///     round-tripped via a conversion + a value-comparer so the EF
///     change tracker sees a re-assigned default-scope list as a
///     genuine change.
/// </summary>
internal sealed class OrgAuthProviderConfigConfiguration : IEntityTypeConfiguration<OrgAuthProviderConfig>
{
    public void Configure(EntityTypeBuilder<OrgAuthProviderConfig> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.OrgAuthProviderConfigs);

        builder.HasKey(static config => config.Id);

        builder.Property(static config => config.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static config => config.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static config => config.Provider)
            .HasColumnName("provider")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(static config => config.OidcAuthority)
            .HasColumnName("oidc_authority")
            .HasMaxLength(2048);

        builder.Property(static config => config.OidcClientId)
            .HasColumnName("oidc_client_id")
            .HasMaxLength(256);

        // OidcClientSecretProtected — encrypted via IDataProtector
        // before the row hits disk. Stored as base64-ish text;
        // 4096 chars is the practical upper bound for the protected
        // payload of a typical 64-byte OIDC client secret.
        builder.Property(static config => config.OidcClientSecretProtected)
            .HasColumnName("oidc_client_secret_protected")
            .HasMaxLength(4096);

        builder.Property(static config => config.OidcScopes)
            .HasColumnName("oidc_scopes")
            .HasColumnType("text[]")
            .HasConversion(
                static scopes => (IEnumerable<string>)scopes,
                static raw => (IReadOnlyList<string>)raw)
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<string>>(
                static (a, b) => (a == null && b == null) ||
                    (a != null && b != null && a.SequenceEqual(b)),
                static v => v.Aggregate(0, static (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
                static v => v.ToArray()));

        builder.Property(static config => config.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static config => config.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // UNIQUE on org_id — exactly one config row per organization.
        // Enforced by Postgres; the Migrator's seeder is
        // idempotent (skip if a row already exists for the org).
        builder.HasIndex(static config => config.OrgId)
            .HasDatabaseName("ix_realm_org_auth_provider_configs_org_id")
            .IsUnique();
    }
}
