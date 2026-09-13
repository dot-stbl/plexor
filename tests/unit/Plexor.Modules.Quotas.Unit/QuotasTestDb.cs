// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotasTestDb — factory for an isolated in-memory QuotasDbContext.
// Each call mints a fresh database (unique name) so tests don't share
// state. InMemory provider ignores PostgreSQL-specific column types
// (varchar vs text) — fine for resolver / catalog logic tests; the
// advisory-lock + scope walker round-trip is covered by integration
// tests against real Postgres in a follow-up.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Infrastructure.Persistence;

namespace Plexor.Modules.Quotas.Unit;

internal static class QuotasTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="QuotasDbContext" /> with
    ///     the schema seeded.
    /// </summary>
    public static async Task<QuotasDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<QuotasDbContext>()
            .UseInMemoryDatabase($"quotas-test-{Guid.NewGuid():N}")
            .Options;
        var db = new QuotasDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
