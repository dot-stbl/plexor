// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotasDbContext — EF Core context for the Quotas module. Owns
// quota_definitions, quota_assignments, quota_usage, and
// rate_limit_events in the 'quotas' PostgreSQL schema. The schema
// name + table names live in the module-local
// Plexor.Modules.Quotas.Infrastructure.Persistence.DatabaseInformation;
// nothing is hard-coded here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Quotas.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Quotas module. Persists the catalog,
///     assignments, usage snapshots, and rate-limit events in the
///     <c>quotas</c> PostgreSQL schema (architecture theme — see
///     <c>AGENTS.md</c> for the schema-vs-concept naming map).
/// </summary>
/// <param name="options">EF Core options bag.</param>
public sealed class QuotasDbContext(DbContextOptions<QuotasDbContext> options) : PlexorDbContext(options)
{
    /// <summary>QuotaDefinitions (quotas.quota_definitions) — stable catalog of limit keys.</summary>
    public DbSet<QuotaDefinition> QuotaDefinitions => Set<QuotaDefinition>();

    /// <summary>QuotaAssignments (quotas.quota_assignments) — polymorphic scope → value bindings.</summary>
    public DbSet<QuotaAssignment> QuotaAssignments => Set<QuotaAssignment>();

    /// <summary>QuotaUsage (quotas.quota_usage) — current consumption snapshot per (scope, definition).</summary>
    public DbSet<QuotaUsage> QuotaUsage => Set<QuotaUsage>();

    /// <summary>RateLimitEvents (quotas.rate_limit_events) — append-only sliding-window event log.</summary>
    public DbSet<RateLimitEvent> RateLimitEvents => Set<RateLimitEvent>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Quotas)
            .ApplyConfiguration(new QuotaDefinitionConfiguration())
            .ApplyConfiguration(new QuotaAssignmentConfiguration())
            .ApplyConfiguration(new QuotaUsageConfiguration())
            .ApplyConfiguration(new RateLimitEventConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
