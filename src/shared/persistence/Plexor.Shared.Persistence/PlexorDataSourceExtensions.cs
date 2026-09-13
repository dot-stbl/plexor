// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorDataSourceExtensions — single shared NpgsqlDataSource registration.
// All module DbContexts draw connections from the same physical pool, so
// EF Core's per-connection transaction scope lines up across contexts.
// Required for cross-DbContext transactions (the 4.5.c quota enforcer
// runs its UPDATE on quotas.quota_usage inside the resource-create
// transaction opened by ClusterDbContext / WorkloadDbContext).
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Plexor.Shared.Persistence;

/// <summary>
///     DI extension that builds and registers a single shared
///     <see cref="NpgsqlDataSource" />. Every PlexorDbContext registered
///     through the data-source overload of
///     <see cref="PlexorPersistenceServiceCollectionExtensions.AddModuleDbContext{TContext}(IServiceCollection, NpgsqlDataSource)" />
///     draws from this pool, so an open transaction on one context is
///     observable from another.
/// </summary>
/// <remarks>
///     <para><b>Why a singleton data source.</b> Postgres advisory locks
///     (<c>pg_advisory_xact_lock</c>) are connection-scoped, not
///     database-scoped — the lock is bound to the physical connection
///     that called it. With two independent connection pools (one per
///     DbContext), a lock taken on the quotas pool would not protect
///     a row read on the clusters pool, and the UPDATE that the
///     enforcer runs would commit (or roll back) on a different
///     connection than the INSERT that the resource-create handler
///     runs. The two writes cannot be atomic without a shared pool.</para>
///     <para><b>What AddPlexorDataSource returns.</b> The constructed
///     <see cref="NpgsqlDataSource" /> — composition roots pass it
///     directly to <c>AddModuleDbContext&lt;T&gt;(dataSource)</c>
///     without an intermediate <c>BuildServiceProvider()</c> call.
///     The same instance is registered as a singleton in DI so anything
///     else that needs the data source can resolve it.</para>
///     <para><b>Connection-string form still exists.</b>
///     <see cref="PlexorPersistenceServiceCollectionExtensions.AddModuleDbContext{TContext}(IServiceCollection, string)" />
///     builds its own data source internally and is fine for tests
///     or one-off scripts that don't need cross-DbContext
///     transactions. Production composition roots use the
///     data-source overload.</para>
/// </remarks>
public static class PlexorDataSourceExtensions
{
    /// <summary>
    ///     Build a single <see cref="NpgsqlDataSource" /> from the supplied
    ///     connection string, register it as a singleton, and return the
    ///     instance so the caller can pass it to
    ///     <see cref="PlexorPersistenceServiceCollectionExtensions.AddModuleDbContext{TContext}(Microsoft.Extensions.DependencyInjection.IServiceCollection, Npgsql.NpgsqlDataSource)" />
    ///     for every PlexorDbContext.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <param name="connectionString">
    ///     PostgreSQL connection string (loaded from the
    ///     <c>ConnectionStrings:Postgres</c> config section by the host).
    /// </param>
    /// <returns>
    ///     The constructed <see cref="NpgsqlDataSource" /> — pass it
    ///     directly to <c>AddModuleDbContext&lt;T&gt;(dataSource)</c>.
    /// </returns>
    public static NpgsqlDataSource AddPlexorDataSource(
        this IServiceCollection services,
        string connectionString)
    {
        var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
        services.AddSingleton(dataSource);
        return dataSource;
    }
}
