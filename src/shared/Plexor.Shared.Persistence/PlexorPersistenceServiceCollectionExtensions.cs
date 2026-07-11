using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Shared.Persistence;

/// <summary>
///     DI extension that registers a module's <see cref="PlexorDbContext" />
///     with the snake_case naming convention, PostgreSQL provider, and a
///     standard set of EF Core interceptors. One extension per DbContext;
///     call from the module's installer.
/// </summary>
/// <remarks>
///     <para><b>Why one extension.</b> Every DbContext needs the same
///     infrastructure (snake_case, Postgres, change-tracking conventions,
///     interceptors). Centralising the wiring means there is exactly one
///     place to extend when we add a new module-wide behaviour (e.g.
///     multi-tenancy filter, audit field auto-population).</para>
///     <para><b>Why no <c>AddDbContext</c> directly.</b> Direct
///     <c>AddDbContext</c> skips the naming convention and lets module
///     code diverge silently from the schema conventions. <c>AddModuleDbContext</c>
///     enforces both.</para>
///     <para><b>Scoped lifetime.</b> Matches EF Core's requirement —
///     DbContext is not thread-safe, must be per-request.</para>
/// </remarks>
public static class PlexorPersistenceServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <typeparamref name="TContext" /> as a scoped DbContext
    ///     with PostgreSQL provider, snake_case naming convention, and the
    ///     standard interceptor set.
    /// </summary>
    /// <typeparam name="TContext">Concrete <see cref="PlexorDbContext" />
    /// subclass for the module.</typeparam>
    /// <param name="services">DI service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string
    /// (typically read from <c>IOptions&lt;ConnectionStringsOptions&gt;</c>
    /// in the host's composition root).</param>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string connectionString)
        where TContext : PlexorDbContext
    {
        services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
            });

            // snake_case naming convention (column + table). Runtime safety-net;
            // design-time correctness is enforced via HasColumnName("snake_case")
            // in every entity's configuration (see coding/ef-core.md).
            options.UseSnakeCaseNamingConvention();

            // Standard interceptor set. Add new entries here, never at the
            // call-site — keeping the wiring centralised means we can audit
            // every interceptor once.
            options.ConfigureWarnings(static w => w.Default(WarningBehavior.Throw));
        });

        return services;
    }
}
