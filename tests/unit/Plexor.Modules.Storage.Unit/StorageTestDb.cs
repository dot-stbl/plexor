// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StorageTestDb — factory for an isolated in-memory StorageDbContext.
// Each call mints a fresh database (unique name) so tests don't share
// state. Mirrors BrandingTestDb / QuotasTestDb; the InMemory provider
// ignores Postgres-specific column types (varchar vs text) — fine for
// the IStorageQuotaReader aggregate tests that exercise the
// COUNT(*) + SUM(size_gb) dance on storage.volumes.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Storage.Infrastructure.Persistence;

namespace Plexor.Modules.Storage.Unit;

internal static class StorageTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="StorageDbContext" />.
    /// </summary>
    public static StorageDbContext Create()
    {
        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseInMemoryDatabase($"storage-test-{Guid.NewGuid():N}")
            .Options;
        return new StorageDbContext(options);
    }
}
