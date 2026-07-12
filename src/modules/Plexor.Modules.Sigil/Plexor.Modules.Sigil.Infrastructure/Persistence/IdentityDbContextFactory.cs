// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentityDbContextFactory — EF Core design-time factory.
//
// IDesignTimeDbContextFactory<TContext> is the standard hook for
// `dotnet ef migrations add` / `dotnet ef database update` to instantiate
// a context without going through the runtime DI container. We use it
// here because the runtime installer (AddSigilInfrastructureCore) registers
// a chain of services that don't all resolve in design time — most
// notably IHttpContextAccessor-backed ICurrentUser (HTTP-only) and a
// scoped ISigningKeyRepository consumed by a singleton IJwtSigningService
// (captive-dependency validation trip in design time).
//
// Connection string is read from the MIGRATOR_CONNECTION environment
// variable (or ConnectionStrings:Postgres from appsettings.json) so
// that local CLI invocations and CI both work without code edits.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Plexor.Modules.Sigil.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="IdentityDbContext" />.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    /// <inheritdoc />
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? config.GetConnectionString("Postgres")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new IdentityDbContext(options);
    }
}
