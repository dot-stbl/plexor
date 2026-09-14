// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EF Core design-time factory for BrandingDbContext. Same pattern as
// QuotasDbContextFactory — `dotnet ef migrations add` and
// `dotnet ef database update` instantiate a context without going
// through runtime DI; this factory provides the minimal hook.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plexor.Modules.Branding.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="BrandingDbContext" />.
///     Resolves the connection from <c>MIGRATOR_CONNECTION</c> and
///     falls back to localhost when the env var is missing.
/// </summary>
public sealed class BrandingDbContextFactory : IDesignTimeDbContextFactory<BrandingDbContext>
{
    /// <inheritdoc />
    public BrandingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<BrandingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BrandingDbContext(options);
    }
}