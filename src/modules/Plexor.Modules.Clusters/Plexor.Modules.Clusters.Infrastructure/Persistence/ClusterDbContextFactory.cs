// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterDbContextFactory — EF Core design-time factory.
//
// See IdentityDbContextFactory / RealmDbContextFactory for the
// rationale (runtime DI chain doesn't resolve in design time; ef
// tool needs a plain factory hook). Same connection-string resolution
// as its siblings: MIGRATOR_CONNECTION env var first, then a
// localhost fallback.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="ClusterDbContext" />.
/// </summary>
public sealed class ClusterDbContextFactory : IDesignTimeDbContextFactory<ClusterDbContext>
{
    /// <inheritdoc />
    public ClusterDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<ClusterDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ClusterDbContext(options);
    }
}
