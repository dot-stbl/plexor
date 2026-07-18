using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Shared.Persistence;

/// <summary>
///     DI extension that registers a <see cref="PlexorDbContext" />
///     subclass with the snake_case naming convention, PostgreSQL
///     provider, and a standard set of EF Core interceptors.
/// </summary>
/// <remarks>
///     <para><b>Composition-root registration.</b>
///     Each host entry-point (Plexor.Host, Plexor.Migrator) registers
///     every <see cref="PlexorDbContext" /> subclass explicitly via
///     <see cref="AddModuleDbContext{TContext}" />. Explicit over
///     reflection: the compiler enforces that new contexts land in
///     every composition root that needs them — no silent miss.
///     The FK-dependent order (Realm → Identity → Clusters → Mtls)
///     must be preserved.</para>
///     <para><b>Why a single connection string across modules.</b>
///     Plexor runs a single PostgreSQL cluster with one database per
///     install. Modules are isolated by <em>schema</em> (sigil / realm /
///     atlas / ...), not by database. One connection string is enough;
///     two would just split the connection pool across strings pointing
///     at the same server.</para>
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
    /// <typeparam name="TContext">
    ///     Concrete <see cref="PlexorDbContext" /> subclass.
    /// </typeparam>
    /// <param name="services">DI service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string connectionString)
            where TContext : PlexorDbContext
    {
        services.AddDbContext<TContext>((sp, options) =>
        {
            _ = sp; // Action<IServiceProvider, DbContextOptionsBuilder> signature required by EF.
            options.UseNpgsql(connectionString,
                npg => npg.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name));

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


