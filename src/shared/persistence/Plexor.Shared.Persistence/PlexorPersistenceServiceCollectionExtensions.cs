using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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
///     <see cref="AddModuleDbContext{TContext}(IServiceCollection, NpgsqlDataSource)" />.
///     Explicit over reflection: the compiler enforces that new
///     contexts land in every composition root that needs them — no
///     silent miss. The FK-dependent order (Realm → Identity →
///     Clusters → Mtls → Quotas) must be preserved.</para>
///     <para><b>Why a single connection pool across modules.</b>
///     Plexor runs a single PostgreSQL cluster with one database per
///     install. Modules are isolated by <em>schema</em> (sigil / realm /
///     atlas / quotas / ...), not by database. A single
///     <see cref="NpgsqlDataSource" /> is shared across every DbContext
///     so cross-DbContext transactions work — the 4.5.c quota enforcer
///     takes a <c>pg_advisory_xact_lock</c> + runs an UPDATE on
///     <c>quotas.quota_usage</c> inside the resource-create
///     transaction opened on <c>ClusterDbContext</c>; both writes commit
///     (or roll back) together because they share a physical connection.
///     Without a shared data source the lock would be released at the
///     wrong scope and the counter would land before the resource row.</para>
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
    ///     backed by the supplied <see cref="NpgsqlDataSource" />. All
    ///     contexts drawn from the same data source share a single
    ///     physical connection pool — required for cross-DbContext
    ///     transactions (e.g. the 4.5.c quota enforcer).
    /// </summary>
    /// <typeparam name="TContext">
    ///     Concrete <see cref="PlexorDbContext" /> subclass.
    /// </typeparam>
    /// <param name="services">DI service collection.</param>
    /// <param name="dataSource">
    ///     The shared <see cref="NpgsqlDataSource" /> the context draws
    ///     connections from. Constructed once in the composition root
    ///     via <see cref="PlexorDataSourceExtensions.AddPlexorDataSource" />.
    /// </param>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        NpgsqlDataSource dataSource)
            where TContext : PlexorDbContext
    {
        services.AddDbContext<TContext>((sp, options) =>
        {
            _ = sp; // Action<IServiceProvider, DbContextOptionsBuilder> signature required by EF.
            options.UseNpgsql(dataSource,
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

    /// <summary>
    ///     Registers <typeparamref name="TContext" /> as a scoped DbContext
    ///     with a fresh connection string. Builds an internal
    ///     <see cref="NpgsqlDataSource" /> from the string — use this
    ///     overload for tests / one-off scripts that don't need to share
    ///     a pool with sibling contexts. Production composition roots use
    ///     the <see cref="AddModuleDbContext{TContext}(IServiceCollection, NpgsqlDataSource)" />
    ///     overload + <see cref="PlexorDataSourceExtensions.AddPlexorDataSource" />.
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
        var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
        return services.AddModuleDbContext<TContext>(dataSource);
    }
}
