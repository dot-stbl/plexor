// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IAuditDbContext — narrow surface the Audit helpers depend on. Keeps
// the helpers off the full DbContext API (which makes the helpers
// easier to mock — Substitute.For<IAuditDbContext>() works without
// unsealing AuditDbContext or making non-virtual DbSet properties
// virtually overridable).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Audit.Domain.Entities;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

/// <summary>
///     Narrow persistence surface the Audit helpers depend on. Lets
///     unit tests substitute the seam without unsealing
///     <see cref="AuditDbContext" /> or making the
///     <see cref="DbSet{TEntity}" /> property virtual (sealed
///     classes cannot have virtual members, and NSubstitute can only
///     intercept virtual members on a sealed proxy).
/// </summary>
public interface IAuditDbContext
{
    /// <summary>The <c>atlas.audit_entries</c> table.</summary>
    public DbSet<AuditEntry> AuditEntries { get; }

    /// <summary>Persist all pending tracked changes.</summary>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
