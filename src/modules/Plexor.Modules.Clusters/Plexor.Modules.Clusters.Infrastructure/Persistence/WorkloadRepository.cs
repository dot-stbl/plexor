// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadRepository — per-module subclass of Repository<Workload>.
// All read paths go through here (with spec + projection); the
// Create + Delete handlers use ClusterDbContext directly because
// they own the transactional boundary.
// ==========================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     Read repository for <see cref="Workload" />. Backs the
///     list-by-cluster + get-by-id endpoints.
/// </summary>
/// <param name="db">The shared <c>forge</c> schema DbContext.</param>
public sealed class WorkloadRepository(ClusterDbContext db) : Repository<Workload>
{
    /// <inheritdoc />
    protected override IQueryable<Workload> Query => db.Workloads;
}