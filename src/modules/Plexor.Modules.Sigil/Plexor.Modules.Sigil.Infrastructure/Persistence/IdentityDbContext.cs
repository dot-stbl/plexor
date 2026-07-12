// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentityDbContext — EF Core context for the Identity module. Persists
// users, roles, role_bindings, refresh_tokens, api_keys, ssh_keys,
// signing_keys in the 'sigil' PostgreSQL schema (schema-per-module
// convention per .agents/STATE.md). All column names + the schema
// constant live in Plexor.Shared.Persistence.DatabaseInformation;
// nothing is hard-coded here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence;

#pragma warning disable CS1591 // XML doc — see DbContext.Set<T>() for usage

public sealed class IdentityDbContext : PlexorDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleBinding> RoleBindings => Set<RoleBinding>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SshKey> SshKeys => Set<SshKey>();
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Identity);
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new RoleBindingConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new ApiKeyConfiguration());
        modelBuilder.ApplyConfiguration(new SshKeyConfiguration());
        modelBuilder.ApplyConfiguration(new SigningKeyConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

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

        // Email is unique per organization (cross-org users are Phase 2).
        builder.HasIndex(static user => new { user.OrgId, user.Email })
            .HasDatabaseName("ix_sigil_users_org_id_email")
            .IsUnique();

        // Org-scoped user list queries (admin endpoints).
        builder.HasIndex(static user => new { user.OrgId, user.Status })
            .HasDatabaseName("ix_sigil_users_org_id_status");
    }
}

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
                static raw => (IReadOnlyList<PermissionScope>)raw.Select(
                    static value => new PermissionScope(value)).ToArray())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<PermissionScope>>(
                static (a, b) => (a == null && b == null) ||
                    (a != null && b != null && a.SequenceEqual(b)),
                static v => v.Aggregate(0, (acc, p) => HashCode.Combine(acc, p.GetHashCode())),
                static v => (IReadOnlyList<PermissionScope>)v.Select(
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
            .HasConversion(
                static permissions => permissions.Select(static p => p.Value).ToArray(),
                static raw => (IReadOnlyList<PermissionScope>)raw.Select(
                    static value => new PermissionScope(value)).ToArray())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<PermissionScope>>(
                static (a, b) => (a == null && b == null) ||
                    (a != null && b != null && a.SequenceEqual(b)),
                static v => v.Aggregate(0, (acc, p) => HashCode.Combine(acc, p.GetHashCode())),
                static v => (IReadOnlyList<PermissionScope>)v.Select(
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