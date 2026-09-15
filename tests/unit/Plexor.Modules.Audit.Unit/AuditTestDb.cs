// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditTestDb — factory for an isolated in-memory AuditDbContext. Each
// call mints a fresh database (unique name) so tests don't share
// state. The InMemory provider ignores PostgreSQL-specific column types
// (jsonb, uuid) — fine for DbAuditEmitter unit tests that exercise
// the INSERT path + the swallow-on-exception contract.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Audit.Infrastructure.Persistence;

namespace Plexor.Modules.Audit.Unit;

/// <summary>
///     In-memory factory for <see cref="AuditDbContext" />. Same
///     pattern as <c>QuotasTestDb</c> / <c>BrandingTestDb</c>.
/// </summary>
internal static class AuditTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="AuditDbContext" /> with
    ///     the schema created.
    /// </summary>
    public static async Task<AuditDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase($"audit-test-{Guid.NewGuid():N}")
            .Options;
        var db = new AuditDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
