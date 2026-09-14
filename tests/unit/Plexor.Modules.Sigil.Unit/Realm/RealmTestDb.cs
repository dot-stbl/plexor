// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RealmTestDb — local copy of the Realm test factory, used by the
// Sigil auth-provider tests to seed OrgAuthProviderConfig rows.
// Duplicated from tests/unit/Plexor.Modules.Realm.Unit/RealmTestDb.cs
// rather than adding a cross-test-project reference; the factory is
// 30 lines and the alternative pulls the Realm.Unit project into the
// Sigil.Unit dependency graph for no benefit.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Realm.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Unit.Realm;

/// <summary>
///     Test factory for <see cref="RealmDbContext" />. Each call mints
///     a fresh in-memory database so the Sigil auth-provider tests
///     start from a clean <c>OrgAuthProviderConfig</c> table.
/// </summary>
internal static class RealmTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="RealmDbContext" /> with
    ///     the schema seeded.
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
