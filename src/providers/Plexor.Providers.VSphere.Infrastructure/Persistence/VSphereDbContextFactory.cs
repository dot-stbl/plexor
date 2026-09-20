// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereDbContextFactory — EF Core design-time factory. `dotnet ef
// migrations add` instantiates a context without going through runtime
// DI; this factory provides the minimal hook (MIGRATOR_CONNECTION env
// var with a localhost fallback).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="VSphereDbContext" />.
///     Resolves the connection from <c>MIGRATOR_CONNECTION</c> and
///     falls back to localhost when the env var is missing.
/// </summary>
public sealed class VSphereDbContextFactory : IDesignTimeDbContextFactory<VSphereDbContext>
{
    /// <inheritdoc />
    public VSphereDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<VSphereDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new VSphereDbContext(options);
    }
}
