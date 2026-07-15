// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClustersInfrastructureInstaller — DI registration for the Clusters
// Infrastructure layer. Registers every cluster + node command
// handler. Called by the Host composition root AFTER
// AddClustersApplicationCore.
// ============================================================================
//
// NOTE: ClusterDbContext itself is registered centrally by
// AddPlexorModuleDbContexts (see Host Program.cs + Migrator Program.cs).
// That helper reflects every PlexorDbContext subclass in
// Plexor.Modules.* assemblies and calls AddModuleDbContext<TContext>
// with the shared Postgres connection string. Re-registering here
// would double-register and cause scope/conflict errors.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Installers;

/// <summary>
///     DI registration extension for the Clusters Infrastructure layer.
/// </summary>
public static class ClustersInfrastructureInstaller
{
    /// <summary>
    ///     Register every cluster + node command handler. The
    ///     <see cref="Persistence.ClusterDbContext" /> itself is
    ///     registered centrally by
    ///     <c>AddPlexorModuleDbContexts</c> — do not re-register here.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">App configuration (unused in v0.1;
    /// reserved for future options-bound module config).</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddClustersInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ICommandHandler<CreateClusterCommand, JoinTokenResult>, CreateClusterCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateClusterCommand, ClusterSummary>, UpdateClusterCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteClusterCommand, Unit>, DeleteClusterCommandHandler>();
        services.AddScoped<ICommandHandler<GetClusterQuery, ClusterDetail>, GetClusterQueryHandler>();
        services.AddScoped<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>, ListClustersQueryHandler>();
        services.AddScoped<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>, RotateJoinTokenCommandHandler>();
        services.AddScoped<ICommandHandler<NodeJoinCommand, NodeJoinResult>, NodeJoinCommandHandler>();
        services.AddScoped<ICommandHandler<NodeHeartbeatCommand, NodeHeartbeatResult>, NodeHeartbeatCommandHandler>();
        services.AddScoped<ICommandHandler<ListNodesQuery, IReadOnlyList<NodeSummary>>, ListNodesQueryHandler>();

        // Read repositories — base class from Shared.Persistence; per-module
        // subclass wires the typed DbSet. Scoped lifetime matches DbContext.
        services.AddScoped<Repository<Cluster>, ClusterRepository>();
        services.AddScoped<Repository<Node>, NodeRepository>();
        services.AddScoped<Repository<JoinToken>, JoinTokenRepository>();

        // Mapperly source-generated DTO mapper. Singleton — generated
        // methods are stateless and the generator is allocation-free.
        // Interface (IClusterMapper) decouples handlers from the
        // concrete Mapperly-generated class so tests can swap in
        // NSubstitute mocks.
        services.AddSingleton<IClusterMapper, ClusterMapper>();

        // Per-entity filter fields — repository reflection-builds the
        // schema once and caches. Singleton = built once, immutable.
        services.AddSingleton(
            static sp => FilterableFieldRegistry.For<Cluster>());

        return services;
    }
}
