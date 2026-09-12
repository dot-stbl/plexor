// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotasDbContextFactory — EF Core design-time factory.
//
// `dotnet ef migrations add` and `dotnet ef database update` instantiate
// a context without going through the runtime DI container. This
// factory provides the minimal hook: read the connection string from
// MIGRATOR_CONNECTION (or fall back to localhost), build the options,
// and return a new context.
//
// Keep this in sync with the sibling factories (Identity, Cluster,
// Realm) — same resolution order, same fallback.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plexor.Modules.Quotas.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="QuotasDbContext" />.
/// </summary>
public sealed class QuotasDbContextFactory : IDesignTimeDbContextFactory<QuotasDbContext>
{
    /// <inheritdoc />
    public QuotasDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<QuotasDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new QuotasDbContext(options);
    }
}
