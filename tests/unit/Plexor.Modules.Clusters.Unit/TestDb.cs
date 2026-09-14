// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TestDb — factory for isolated ClusterDbContext instances.
//
// Two providers:
//   CreateAsync()    — InMemory provider. Supports LINQ OrderBy on
//                     DateTimeOffset (which the NodesByClusterSpec uses
//                     for the dashboard node list) but does not support
//                     ExecuteUpdateAsync.
//   SqliteAsync()    — SQLite in-memory provider. Supports
//                     ExecuteUpdateAsync (used by UpdateCluster /
//                     RotateJoinToken handlers) but cannot translate
//                     ORDER BY on DateTimeOffset columns — SQLite
//                     stores DateTimeOffset as TEXT and refuses to
//                     compare.
//
// Tests pick the provider that fits the code path under test:
// read-only tests with ORDER BY use CreateAsync; handlers that hit
// ExecuteUpdateAsync use SqliteAsync.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Infrastructure.Persistence;

namespace Plexor.Modules.Clusters.Unit;

internal static class TestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="ClusterDbContext" /> with
    ///     the schema seeded. Each call mints a fresh database (unique
    ///     name) so tests don't share state.
    /// </summary>
    public static async Task<ClusterDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ClusterDbContext>()
            .UseInMemoryDatabase($"clusters-test-{Guid.NewGuid():N}")
            .Options;
        var db = new ClusterDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    /// <summary>
    ///     Create a fresh SQLite-in-memory <see cref="ClusterDbContext" />
    ///     with the schema seeded. Each call gets a unique shared cache
    ///     name so tests are isolated. Required for handlers that use
    ///     <c>ExecuteUpdateAsync</c> (UpdateCluster, RotateJoinToken),
    ///     which the InMemory provider doesn't support.
    /// </summary>
    public static async Task<ClusterDbContext> SqliteAsync()
    {
        var cacheName = $"clusters-test-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ClusterDbContext>()
            .UseSqlite($"Data Source=file:{cacheName}?mode=memory&cache=shared")
            .Options;
        var db = new ClusterDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}

