// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostInfrastructureInstaller — DI registration for the Outpost
// Infrastructure layer. Registers every Outpost command handler +
// the read repositories + the heartbeat evaluator.
//
// OutpostDbContext itself is registered centrally by the composition
// root (Plexor.Host Program.cs + Plexor.Migrator Program.cs) via an
// explicit AddModuleDbContext<OutpostDbContext>(plexorDataSource) call.
// Re-registering here would double-register — mirrors the Branding /
// Quotas / Clusters pattern.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Modules.Outpost.Application.NodeCommands;
using Plexor.Modules.Outpost.Infrastructure.Nodes;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Outpost.Infrastructure.Installers;

/// <summary>
///     DI registration extension for the Outpost Infrastructure layer.
/// </summary>
public static class OutpostInfrastructureInstaller
{
    /// <summary>
    ///     Register every Outpost command handler + the read
    ///     repositories + the heartbeat evaluator. The
    ///     <see cref="Persistence.OutpostDbContext" /> itself is
    ///     registered centrally by
    ///     <c>AddModuleDbContext&lt;OutpostDbContext&gt;</c> — do not
    ///     re-register.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddOutpostInfrastructureCore(
        this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<RegisterNodeCommand, RegisterNodeResult>, RegisterNodeCommandHandler>();
        services.AddScoped<ICommandHandler<HeartbeatCommand, HeartbeatResult>, HeartbeatCommandHandler>();
        services.AddScoped<ICommandHandler<ListNodesQuery, IReadOnlyList<NodeRecord>>, ListNodesQueryHandler>();
        services.AddScoped<ICommandHandler<GetNodeQuery, NodeRecord?>, GetNodeQueryHandler>();

        // Read repositories — base class from Shared.Persistence;
        // per-module subclass wires the typed DbSet. Scoped
        // lifetime matches DbContext.
        services.AddScoped<Repository<NodeRecord>, Persistence.Repositories.NodeRecordRepository>();

        // Heartbeat evaluator — singleton (config-bound + pure).
        services.AddSingleton<INodeHeartbeatEvaluator, NodeHeartbeatEvaluator>();

        return services;
    }
}