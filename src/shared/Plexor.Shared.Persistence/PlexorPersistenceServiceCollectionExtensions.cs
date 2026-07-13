using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Shared.Persistence;

/// <summary>
///     DI extension that registers one or more <see cref="PlexorDbContext" />
///     subclasses with the snake_case naming convention, PostgreSQL
///     provider, and a standard set of EF Core interceptors.
/// </summary>
/// <remarks>
///     <para><b>One extension, two overloads.</b>
///     <list type="bullet">
///       <item><see cref="AddModuleDbContext{TContext}" /> — register one
///         specific context. Use in module installers when only that
///         module's context is needed.</item>
///       <item><see cref="AddPlexorModuleDbContexts(IServiceCollection, string)" />
///         — scan every <c>Plexor.Modules.*.Infrastructure</c> assembly for
///         <see cref="PlexorDbContext" /> subclasses and register all of
///         them against the same connection string. Use in the host
///         composition root when all modules share a single Postgres
///         instance (the common case — schema-per-module is the
///         isolation primitive, not database-per-module).</item>
///     </list></para>
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

    /// <summary>
    ///     Scans every loaded assembly whose name starts with
    ///     <c>Plexor.Modules.</c> for non-abstract sealed
    ///     <see cref="PlexorDbContext" /> subclasses and registers each one
    ///     with the shared <paramref name="connectionString" />. Single
    ///     Postgres cluster, schema-per-module isolation — one string
    ///     covers all modules.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string. The
    ///     same string is used for every discovered context — no per-module
    ///     config keys.</param>
    /// <returns>Number of contexts registered (diagnostic).</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static int AddPlexorModuleDbContexts(
        this IServiceCollection services,
        string connectionString)
    {
        var contexts = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static a => a.GetName().Name?.StartsWith("Plexor.Modules.", StringComparison.Ordinal) ?? false)
            .SelectMany(static a => a.GetExportedTypes())
            .Where(static t => t.IsClass
                            && !t.IsAbstract
                            && t.IsSealed
                            && typeof(PlexorDbContext).IsAssignableFrom(t))
            .ToList();

        // Dispatch each closed DbContext to AddModuleDbContext<TContext>.
        // Single-pass reflection — one MethodInfo per closed generic form.
        // Equivalent to calling AddModuleDbContext<RealmDbContext>(connectionString)
        // + AddModuleDbContext<IdentityDbContext>(connectionString) + ... inline.
        var addModuleMethod = typeof(PlexorPersistenceServiceCollectionExtensions)
            .GetMethod(nameof(AddModuleDbContext), BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "AddModuleDbContext<TContext> not found via reflection — method renamed?");

        foreach (var ctx in contexts)
        {
            var closedMethod = addModuleMethod.MakeGenericMethod(ctx);
            closedMethod.Invoke(null, [services, connectionString]);
        }

        return contexts.Count;
    }
}
