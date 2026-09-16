// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostDbContext — EF Core context for the Outpost module. Persists
// node_records in the `outpost` PostgreSQL schema (architecture theme).
//
// The schema name + table name live in Plexor.Shared.Persistence
// .DatabaseInformation. Column shape + indexes live in
// Configurations/NodeRecordConfiguration.cs.
//
// Migration history: this is the first Outpost migration. Schema
// dependency order (per AGENTS.md):
//   realm → sigil → atlas → outpost
// The migrator applies them in that order; Outpost has no FK into the
// earlier schemas — node_records.cluster_id is a soft reference
// (varchar(64)) to forge.clusters.id enforced at the application layer.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Outpost.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Outpost module. Persists the host-side
///     node registry (<c>outpost.node_records</c>) — one row per joined
///     Plexor.NodeAgent.
/// </summary>
/// <param name="options">EF Core options bag.</param>
public sealed class OutpostDbContext(DbContextOptions<OutpostDbContext> options)
    : PlexorDbContext(options)
{
    /// <summary>NodeRecords (outpost.node_records) — joined Plexor.NodeAgent instances.</summary>
    public DbSet<NodeRecord> NodeRecords => Set<NodeRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Nodes)
            .ApplyConfiguration(new NodeRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}