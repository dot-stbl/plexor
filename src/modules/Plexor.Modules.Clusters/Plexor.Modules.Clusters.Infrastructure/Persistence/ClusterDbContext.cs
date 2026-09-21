// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterDbContext — EF Core context for the Clusters module. Persists
// clusters, nodes, and join_tokens in the 'forge' PostgreSQL schema
// (schema-per-module per .agents/STATE.md). All column names + the
// schema constant live in Plexor.Shared.Persistence.DatabaseInformation.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Clusters module. Persists clusters,
///     nodes, and join_tokens in the 'forge' PostgreSQL schema
///     (schema-per-module per .agents/STATE.md). All column names +
///     the schema constant live in
///     <c>Plexor.Shared.Persistence.DatabaseInformation</c>.
/// </summary>
/// <param name="options"></param>
public sealed class ClusterDbContext(DbContextOptions<ClusterDbContext> options) : PlexorDbContext(options)
{
    /// <summary>Clusters (forge.clusters) — Plexor.Host + joined nodes, one row per fleet.</summary>
    public DbSet<Cluster> Clusters => Set<Cluster>();
    /// <summary>Nodes (forge.nodes) — joined Plexor.NodeAgent instances.</summary>
    public DbSet<Node> Nodes => Set<Node>();
    /// <summary>JoinTokens (forge.join_tokens) — one-time credentials for first node attach.</summary>
    public DbSet<JoinToken> JoinTokens => Set<JoinToken>();
    /// <summary>Workloads (forge.workloads) — control-plane view of every deployed workload.</summary>
    public DbSet<Workload> Workloads => Set<Workload>();
    /// <summary>Workload lifecycle events (forge.workload_lifecycle_events) — append-only audit trail per Mark* transition.</summary>
    public DbSet<WorkloadLifecycleEvent> WorkloadLifecycleEvents => Set<WorkloadLifecycleEvent>();
    /// <summary>NodeCommands (forge.commands) — per-node command queue; agent long-polls and posts results back.</summary>
    public DbSet<NodeCommand> Commands => Set<NodeCommand>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Clusters)
            .ApplyConfiguration(new ClusterConfiguration())
            .ApplyConfiguration(new NodeConfiguration())
            .ApplyConfiguration(new JoinTokenConfiguration())
            .ApplyConfiguration(new WorkloadConfiguration())
            .ApplyConfiguration(new WorkloadLifecycleEventConfiguration())
            .ApplyConfiguration(new NodeCommandConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
