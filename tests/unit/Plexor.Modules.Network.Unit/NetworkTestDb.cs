// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkTestDb — factory for an isolated in-memory NetworkDbContext.
// Each call mints a fresh database (unique name) so tests don't share
// state. Mirrors BrandingTestDb / StorageTestDb.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Network.Infrastructure.Persistence;

namespace Plexor.Modules.Network.Unit;

internal static class NetworkTestDb
{
    /// <summary>
    ///     Create a fresh in-memory <see cref="NetworkDbContext" />.
    /// </summary>
    public static NetworkDbContext Create()
    {
        var options = new DbContextOptionsBuilder<NetworkDbContext>()
            .UseInMemoryDatabase($"network-test-{Guid.NewGuid():N}")
            .Options;
        return new NetworkDbContext(options);
    }
}
