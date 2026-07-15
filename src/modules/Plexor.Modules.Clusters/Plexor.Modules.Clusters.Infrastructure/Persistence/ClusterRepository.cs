// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterRepository — per-module subclass of Repository<Cluster>.
// Wires the typed DbSet; defaults to delegating each method to the
// base. Customize here only when genuinely cluster-specific (none
// for v0.1).
// ============================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     Read repository for <see cref="Cluster" />. Write paths stay on
///     <c>ClusterDbContext</c> directly via the command handlers —
///     see <c>architecture/persistence.md</c>.
/// </summary>
/// <param name="db">The shared <c>forge</c> schema DbContext.</param>
public sealed class ClusterRepository(ClusterDbContext db) : Repository<Cluster>
{
    /// <inheritdoc />
    protected override IQueryable<Cluster> Query => db.Clusters;
}
