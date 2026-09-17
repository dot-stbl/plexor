// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EF Core design-time factory for StorageDbContext. Same pattern as
// QuotasDbContextFactory / BrandingDbContextFactory — `dotnet ef
// migrations add` and `dotnet ef database update` instantiate a context
// without going through runtime DI; this factory provides the minimal
// hook (MIGRATOR_CONNECTION env var with a localhost fallback).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plexor.Modules.Storage.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="StorageDbContext" />.
///     Resolves the connection from <c>MIGRATOR_CONNECTION</c> and
///     falls back to localhost when the env var is missing.
/// </summary>
public sealed class StorageDbContextFactory : IDesignTimeDbContextFactory<StorageDbContext>
{
    /// <inheritdoc />
    public StorageDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StorageDbContext(options);
    }
}
