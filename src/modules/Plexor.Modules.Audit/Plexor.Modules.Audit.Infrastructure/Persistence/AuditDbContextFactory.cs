using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

/// <summary>
///     EF Core design-time factory for <see cref="AuditDbContext" />.
///     The runtime DI chain doesn't resolve in design time — the
///     <c>dotnet ef</c> tool needs a plain factory hook that returns
///     a <see cref="AuditDbContext" /> with a concrete
///     <see cref="DbContextOptions{TContext}" />. Runtime registration
///     happens through <c>AddModuleDbContext&lt;AuditDbContext&gt;</c>
///     in the composition roots (Plexor.Host / Plexor.Migrator).
/// </summary>
/// <remarks>
///     <para><b>Connection string.</b> The factory honours
///     <c>MIGRATOR_CONNECTION</c> (the same env var the Migrator
///     CLI reads). When unset, falls back to a localhost default
///     so the <c>dotnet ef</c> tool can build the model in any
///     environment — the tool never actually opens a connection
///     at design time, so the value is symbolic.</para>
///     <para><b>Migrations assembly.</b>
///     <c>UseNpgsql(connectionString)</c> without an explicit
///     <c>MigrationsAssembly(...)</c> — the migrations live in this
///     same assembly (Plexor.Modules.Audit.Infrastructure), which
///     is the default for EF Core when the DbContext is in the same
///     assembly as the migration.</para>
/// </remarks>
public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    /// <inheritdoc />
    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=plexor;Username=plexor;Password=plexor";

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AuditDbContext(options);
    }
}
