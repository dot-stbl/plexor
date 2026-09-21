// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentityDbContext — EF Core context for the Identity module. Persists
// users, roles, role_bindings, refresh_tokens, api_keys, ssh_keys,
// signing_keys in the 'sigil' PostgreSQL schema (schema-per-module
// convention per .agents/STATE.md). All column names + the schema
// constant live in Plexor.Shared.Persistence.DatabaseInformation;
// nothing is hard-coded here. Entity-level configuration lives in
// Persistence/Configurations/{User,Role,RoleBinding,RefreshToken,
// ApiKey,SshKey,SigningKey}Configuration.cs — one file per table.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Sigil (identity) module. Owns users,
///     roles, role_bindings, refresh_tokens, api_keys, ssh_keys,
///     and signing_keys in the 'sigil' PostgreSQL schema (schema-per-
///     module convention per .agents/STATE.md).
/// </summary>
/// <param name="options"></param>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : PlexorDbContext(options)
{
    /// <summary>Users (sigil.users) — operator accounts with email + password.</summary>
    public DbSet<User> Users => Set<User>();
    /// <summary>Roles (sigil.roles) — named permission bundles.</summary>
    public DbSet<Role> Roles => Set<Role>();
    /// <summary>RoleBindings (sigil.role_bindings) — user↔role grant with 3-tier scope.</summary>
    public DbSet<RoleBinding> RoleBindings => Set<RoleBinding>();
    /// <summary>RefreshTokens (sigil.refresh_tokens) — JWT refresh, revocable per-family.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    /// <summary>ApiKeys (sigil.api_keys) — long-lived service-account credentials.</summary>
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    /// <summary>SshKeys (sigil.ssh_keys) — public keys for VM access.</summary>
    public DbSet<SshKey> SshKeys => Set<SshKey>();
    /// <summary>SigningKeys (sigil.signing_keys) — JWT signing keys (rotation history).</summary>
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Identity)
            .ApplyConfiguration(new UserConfiguration())
            .ApplyConfiguration(new RoleConfiguration())
            .ApplyConfiguration(new RoleBindingConfiguration())
            .ApplyConfiguration(new RefreshTokenConfiguration())
            .ApplyConfiguration(new ApiKeyConfiguration())
            .ApplyConfiguration(new SshKeyConfiguration())
            .ApplyConfiguration(new SigningKeyConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}