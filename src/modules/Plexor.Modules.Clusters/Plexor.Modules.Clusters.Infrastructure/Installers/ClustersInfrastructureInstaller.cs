// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClustersInfrastructureInstaller — DI registration for the Clusters
// Infrastructure layer. Registers every cluster + node command
// handler. Called by the Host composition root AFTER
// AddClustersApplicationCore.
// ============================================================================
//
// NOTE: ClusterDbContext itself is registered centrally by the
// composition root (Host Program.cs + Migrator Program.cs) via
// an explicit AddModuleDbContext<ClusterDbContext>(connectionString)
// call. Re-registering here would double-register and cause
// scope/conflict errors.

using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Application.CreateVm;
using Plexor.Modules.Clusters.Application.Flavors;
using Plexor.Modules.Clusters.Application.Images;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Placement;
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
    ///     <c>AddModuleDbContext&lt;T&gt;</c> — do not re-register here.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddClustersInfrastructureCore(
        this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateClusterCommand, JoinTokenResult>, CreateClusterCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateClusterCommand, ClusterSummary>, UpdateClusterCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteClusterCommand, Unit>, DeleteClusterCommandHandler>();
        services.AddScoped<ICommandHandler<GetClusterQuery, ClusterDetail>, GetClusterQueryHandler>();
        services.AddScoped<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>, ListClustersQueryHandler>();
        services.AddScoped<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>, RotateJoinTokenCommandHandler>();
        // Node join / heartbeat / list handlers moved to
        // Plexor.Modules.Outpost.Infrastructure as part of the
        // node-tracking extraction. The clusters module still owns
        // the join-token surface (RotateJoinTokenCommand) — that
        // handler validates the token shape + flips the status row
        // when a node redeems it.

        // Workload handlers — see WorkloadCommandHandlers.cs +
        // WorkloadReadHandlers.cs. Create / Delete / List / Get split
        // across two files following the Cluster pattern.
        services.AddScoped<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>, CreateWorkloadCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteWorkloadCommand, Unit>, DeleteWorkloadCommandHandler>();
        services.AddScoped<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>, ListWorkloadsQueryHandler>();
        services.AddScoped<ICommandHandler<GetWorkloadQuery, WorkloadSummary>, GetWorkloadQueryHandler>();

        // VM-specific provisioning (issue #6) — resolves Flavor +
        // Image catalogs and Config overlay into a final
        // VmRuntimeConfig, validates it, pins the target node via
        // the placement scheduler, persists the Workload row, and
        // enqueues a workload.create NodeCommand for the agent.
        services.AddScoped<ICommandHandler<CreateVmCommand, CreateVmResult>, CreateVmHandler>();

        // Tier 5: workload action commands (start / stop / restart).
        // Handler short-polls the per-node command queue (forge.commands)
        // for the agent's ack; control plane returns once the row
        // reaches Acked or Failed. v0.2+ switches to async-fire-and-
        // forget + heartbeat-driven state.
        services.AddScoped<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>, WorkloadActionCommandHandler>();

        // Placement scheduler — picks a node for a new workload.
        // v0.1 = ManualPlacementScheduler (operator pins the node
        // through WorkloadSpec.TargetNodeId; otherwise stays
        // unassigned). Scoped to match the handler lifetime — a
        // future stateful scheduler that tracks per-node capacity
        // may move to Singleton once its state is concurrency-safe.
        services.AddScoped<IPlacementScheduler, ManualPlacementScheduler>();

        // PlacementCandidateLoader projects ClusterDbContext rows into
        // the scheduler's NodeCandidate shape. Scoped — shares the
        // DbContext with the calling handler.
        services.AddScoped<PlacementCandidateLoader>();

        // Read repositories — base class from Shared.Persistence; per-module
        // subclass wires the typed DbSet. Scoped lifetime matches DbContext.
        services.AddScoped<Repository<Cluster>, ClusterRepository>();
        services.AddScoped<Repository<JoinToken>, JoinTokenRepository>();
        services.AddScoped<Repository<Workload>, WorkloadRepository>();

        // Mapperly source-generated DTO mapper. Singleton — generated
        // methods are stateless and the generator is allocation-free.
        // Interface (IClusterMapper) decouples handlers from the
        // concrete Mapperly-generated class so tests can swap in
        // NSubstitute mocks.
        services.AddSingleton<IClusterMapper, ClusterMapper>();
        services.AddSingleton<IWorkloadMapper, WorkloadMapper>();

        // Flavor + Image catalogs (issue #5). Singleton — the v0.1
        // list is immutable for the process lifetime; v0.2+ may
        // swap to a DB-backed catalog that drops to Scoped to
        // follow the DbContext lifetime.
        services.AddSingleton<IFlavorCatalog, DefaultFlavorCatalog>();
        services.AddSingleton<IImageCatalog, DefaultImageCatalog>();

        // Per-entity filter fields — repository reflection-builds the
        // schema once and caches. Singleton = built once, immutable.
        services.AddSingleton(
            static _ => FilterableFieldRegistry.For<Cluster>());
        services.AddSingleton(
            static _ => FilterableFieldRegistry.For<Workload>());

        return services;
    }
}
