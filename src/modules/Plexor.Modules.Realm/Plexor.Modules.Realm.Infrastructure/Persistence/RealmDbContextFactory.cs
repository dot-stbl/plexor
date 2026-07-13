// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RealmDbContextFactory — EF Core design-time factory.
//
// See IdentityDbContextFactory for the rationale (runtime DI chain
// doesn't resolve in design time; ef tool needs a plain factory hook).
//
// Connection string read from MIGRATOR_CONNECTION env var (preferred) or
// ConnectionStrings:Postgres configuration source, with a localhost
// fallback for local CLI runs.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Plexor.Modules.Realm.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="RealmDbContext" />.
/// </summary>
public sealed class RealmDbContextFactory : IDesignTimeDbContextFactory<RealmDbContext>
{
    /// <inheritdoc />
    public RealmDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? config.GetConnectionString("Postgres")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<RealmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RealmDbContext(options);
    }
}
