// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TestDb — factory for an isolated in-memory SQLite-backed
// IdentityDbContext. SQLite is required (not the InMemory provider)
// because the handler relies on ExecuteUpdate / ExecuteDelete, which
// the InMemory provider does not implement.
//
// The production RoleConfiguration maps Role.Permissions (and
// ApiKey.Permissions) as a PostgreSQL text[] column with an
// IReadOnlyList<PermissionScope> ↔ string[] converter. SQLite cannot
// model that converter chain on a collection property — the same
// failure mode the InMemory provider hits during model finalization.
// IdentityDbContext is intentionally unsealed (per naming-and-types.md
// §2 exception #2) so test projects can subclass and override
// OnModelCreating; we apply every production configuration, then
// strip Permissions from Role and ApiKey. The handler never reads
// these columns, so the divergence is invisible to behavioural tests;
// column-shape correctness is covered by integration tests against
// real Postgres.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Unit;

internal sealed class TestIdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext(options)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Applies every production configuration, then strips the
    ///     <c>text[]</c> <see cref="Role.Permissions" /> and
    ///     <c>ApiKey.Permissions</c> columns from the model entirely
    ///     so SQLite doesn't try to compose their converter chains.
    ///     See file header for the full rationale.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>().Ignore(static role => role.Permissions);
        modelBuilder.Entity<ApiKey>().Ignore(static key => key.Permissions);
    }
}

internal static class TestDb
{
    public static async Task<IdentityDbContext> CreateAsync()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestIdentityDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
