// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingTestDb — factory for an isolated in-memory BrandingDbContext.
// Each call mints a fresh database (unique name) so tests don't
// share state. The InMemory provider ignores PostgreSQL-specific
// column types — fine for the IBrandingService unit tests that
// exercise the SELECT/INSERT/UPDATE dance on the global +
// per-org rows.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Branding.Infrastructure.Persistence;

namespace Plexor.Modules.Branding.Unit;

internal static class BrandingTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="BrandingDbContext" />.
    /// </summary>
    public static BrandingDbContext Create()
    {
        var options = new DbContextOptionsBuilder<BrandingDbContext>()
            .UseInMemoryDatabase($"branding-test-{Guid.NewGuid():N}")
            .Options;
        return new BrandingDbContext(options);
    }
}