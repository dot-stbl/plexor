// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RevokedCertsDbContextFactory — EF Core design-time factory.
//
// Mirrors ClusterDbContextFactory / IdentityDbContextFactory: the
// runtime DI chain does not resolve at design time, so the ef tool
// needs a plain factory hook. Connection-string resolution is the
// same as its siblings — MIGRATOR_CONNECTION env var first, then a
// localhost fallback.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Plexor.Shared.Mtls.Persistence;

namespace Plexor.Shared.Mtls;

/// <summary>
///     EF Core design-time factory for
///     <see cref="RevokedCertsDbContext" />.
/// </summary>
public sealed class RevokedCertsDbContextFactory : IDesignTimeDbContextFactory<RevokedCertsDbContext>
{
    /// <inheritdoc />
    public RevokedCertsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<RevokedCertsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RevokedCertsDbContext(options);
    }
}
