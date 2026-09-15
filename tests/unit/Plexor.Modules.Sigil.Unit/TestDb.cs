// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TestDb — factory for an isolated in-memory IdentityDbContext. Each
// call mints a fresh database (unique name) so tests don't share state.
//
// Provider choice: SQLite in-memory. InMemory provider does NOT support
// EF Core's ExecuteUpdate / ExecuteDelete, which the production code
// uses for atomic refresh-token rotation (EfRefreshTokenStore.RotateAsync).
// SQLite in-memory supports those operations and is fast. The
// PostgreSQL-specific column types declared on the entity
// configurations (uuid, text[], timestamptz) are coerced to SQLite
// types via the value converter override below; column-shape
// correctness is covered by integration tests against real Postgres.
// ============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Unit;

/// <summary>
///     Shared <see cref="IdentityDbContext" /> factory for unit tests.
///     Each call returns a fresh SQLite-in-memory database that lives
///     as long as the returned context's connection stays open.
/// </summary>
internal static class TestDb
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private static readonly ValueComparer<IReadOnlyList<PermissionScope>> PermissionsComparer = new(
        static (a, b) => (a == null && b == null)
            || (a != null && b != null && a.SequenceEqual(b)),
        static v => v.Aggregate(0,
            static (acc, p) => HashCode.Combine(acc, p.GetHashCode())),
        static v => v.Select(
            static p => new PermissionScope(p.Value)).ToArray());

    /// <summary>
    ///     Create a fresh <see cref="IdentityDbContext" /> with the
    ///     schema seeded. The connection stays open until the caller
    ///     disposes the context — SQLite-in-memory drops the
    ///     database when the last connection closes.
    /// </summary>
    public static async Task<IdentityDbContext> CreateAsync()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new IdentityDbContext(options, OverridePermissionsMapping);
        await db.Database.EnsureCreatedAsync();

        // Keep the connection alive on the context so callers can
        // dispose normally without losing the database mid-test.
        db.Database.SetDbConnection(connection);
        return db;
    }

    /// <summary>
    ///     Remap <c>Permissions</c> columns to a JSON-encoded string so
    ///     SQLite (without array types) can store them as TEXT while
    ///     EF can still project <c>k.Permissions.Select(...)</c>
    ///     through the value converter.
    /// </summary>
    /// <param name="modelBuilder">EF Core model builder.</param>
    private static void OverridePermissionsMapping(ModelBuilder modelBuilder)
    {
        RemapPermissions<ApiKey>(modelBuilder);
        RemapPermissions<Role>(modelBuilder);
    }

    private static void RemapPermissions<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .Property<IReadOnlyList<PermissionScope>>("Permissions")
            .HasConversion(
                static perms => JsonSerializer.Serialize(
                    perms.Select(static p => p.Value).ToArray(),
                    JsonOptions),
                static raw => JsonSerializer
                    .Deserialize<string[]>(raw, JsonOptions)!
                    .Select(static value => new PermissionScope(value))
                    .ToArray(),
                PermissionsComparer);
    }
}
