// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditDbContext — EF Core context for the Audit module. Owns
// audit_entries in the `atlas` PostgreSQL schema (architecture theme).
// The schema name + table name live in the module-local
// Plexor.Modules.Audit.Infrastructure.Persistence.DatabaseInformation;
// nothing is hard-coded here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Audit.Domain.Entities;
using Plexor.Modules.Audit.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Audit module. Persists the append-only
///     <c>audit_entries</c> event log in the <c>atlas</c> PostgreSQL
///     schema. Composed from a single
///     <see cref="IEntityTypeConfiguration{TEntity}" /> file in
///     <c>Configurations/</c>.
/// </summary>
/// <param name="options">EF Core options bag.</param>
/// <remarks>
///     <b>Not sealed.</b> Left open specifically so the unit-test
///     project can subclass with a <c>SaveChangesAsync</c> override
///     that throws — exercising the DbAuditEmitter's swallow-on-
///     exception contract without needing a real Postgres instance
///     or NSubstitute-on-sealed-class gymnastics. Documented
///     extension point per <c>class-layout-and-tooling.md</c> §1a.
///     Production code never subclasses it.
/// </remarks>
public class AuditDbContext(DbContextOptions<AuditDbContext> options)
    : PlexorDbContext(options)
{
    /// <summary>AuditEntries (atlas.audit_entries) — one row per
    /// audited action.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Audit)
            .ApplyConfiguration(new AuditEntryConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
