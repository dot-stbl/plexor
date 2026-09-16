// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterTestDb — factory for an isolated in-memory ClusterDbContext.
// Used by Outpost tests that need a Cluster row + active JoinToken
// to drive the RegisterNodeCommandHandler. Each call mints a
// fresh database so tests don't share state.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Infrastructure.Persistence;

namespace Plexor.Modules.Outpost.Unit;

internal static class ClusterTestDb
{
    public static async Task<ClusterDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ClusterDbContext>()
            .UseInMemoryDatabase($"cluster-test-outpost-{Guid.NewGuid():N}")
            .Options;
        var db = new ClusterDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}