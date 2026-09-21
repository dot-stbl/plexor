// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereTestDb — factory for an isolated in-memory VSphereDbContext. Each
// call mints a fresh database (unique name) so tests don't share state.
// The InMemory provider ignores PostgreSQL-specific column types
// (uuid, jsonb) — fine for the inventory refresher + provisioning
// service tests that exercise the INSERT / SaveChanges path with a
// mocked Refit client. Transactions are not supported by the
// in-memory provider; the tests configure the warning so the
// inventory refresher's atomic-replace transaction becomes a
// no-op (the test exercises the row-replacement logic, not the
// transaction semantics).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Plexor.Providers.VSphere.Infrastructure.Persistence;

namespace Plexor.Providers.VSphere.Unit;

/// <summary>
///     In-memory factory for <see cref="VSphereDbContext" />.
/// </summary>
internal static class VSphereTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="VSphereDbContext" />
    ///     with the schema created. The InMemory provider does not
    ///     support transactions — the
    ///     <see cref="InMemoryEventId.TransactionIgnoredWarning" />
    ///     is suppressed so the inventory refresher's
    ///     atomic-replace transaction becomes a no-op in tests.
    /// </summary>
    public static async Task<VSphereDbContext> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<VSphereDbContext>()
            .UseInMemoryDatabase($"vsphere-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new VSphereDbContext(options);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        return db;
    }
}
