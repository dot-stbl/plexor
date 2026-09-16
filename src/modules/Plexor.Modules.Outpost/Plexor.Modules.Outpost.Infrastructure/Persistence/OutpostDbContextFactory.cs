// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostDbContextFactory — EF Core design-time factory.
//
// Same pattern as AuditDbContextFactory / ClusterDbContextFactory:
// `dotnet ef migrations add` and `dotnet ef database update` instantiate
// a context without going through runtime DI; this factory provides the
// minimal hook (MIGRATOR_CONNECTION env var with a localhost fallback).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plexor.Modules.Outpost.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="OutpostDbContext" />.
///     Resolves the connection from <c>MIGRATOR_CONNECTION</c> and
///     falls back to localhost when the env var is missing.
/// </summary>
public sealed class OutpostDbContextFactory : IDesignTimeDbContextFactory<OutpostDbContext>
{
    /// <inheritdoc />
    public OutpostDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<OutpostDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OutpostDbContext(options);
    }
}