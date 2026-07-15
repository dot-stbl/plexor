// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TestDb — factory for an isolated in-memory ClusterDbContext. Each
// call mints a fresh database (unique name) so tests don't share state.
// InMemory provider ignores PostgreSQL-specific column types (text[],
// jsonb) — fine for handler logic tests; column-shape correctness is
// covered by the integration tests against real Postgres.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Infrastructure.Persistence;

namespace Plexor.Modules.Clusters.Unit;

internal static class TestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="ClusterDbContext" /> with
    ///     the schema seeded.
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
}
