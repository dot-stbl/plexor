// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RealmTestDb — factory for an isolated in-memory RealmDbContext.
// Each call mints a fresh database (unique name) so tests don't
// share state. InMemory provider ignores PostgreSQL-specific column
// types (varchar vs text) — fine for the seeder's read + insert
// round-trip; the Postgres UNIQUE constraint is exercised by the
// migrator's integration tests in a follow-up.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Realm.Infrastructure.Persistence;

namespace Plexor.Modules.Realm.Unit;

/// <summary>
///     Test factory for <see cref="RealmDbContext" />. Each call
///     mints a fresh in-memory database; tests that need to seed
///     orgs + read back rows do so against the same instance.
/// </summary>
internal static class RealmTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="RealmDbContext" />
    ///     with the schema seeded.
    /// </summary>
    public static async Task<RealmDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<RealmDbContext>()
            .UseInMemoryDatabase($"realm-test-{Guid.NewGuid():N}")
            .Options;
        var db = new RealmDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
