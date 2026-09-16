// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostTestDb — factory for an isolated in-memory OutpostDbContext.
// Each call mints a fresh database (unique name) so tests don't share
// state. The InMemory provider ignores PostgreSQL-specific column
// types (jsonb, uuid) — fine for handler logic tests; column-shape
// correctness is covered by the migration in the Infrastructure
// project.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Outpost.Infrastructure.Persistence;

namespace Plexor.Modules.Outpost.Unit;

/// <summary>
///     In-memory factory for <see cref="OutpostDbContext" />. Same
///     pattern as AuditTestDb / QuotasTestDb / TestDb (Clusters).
/// </summary>
internal static class OutpostTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="OutpostDbContext" />
    ///     with the schema seeded.
    /// </summary>
    public static async Task<OutpostDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<OutpostDbContext>()
            .UseInMemoryDatabase($"outpost-test-{Guid.NewGuid():N}")
            .Options;
        var db = new OutpostDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}