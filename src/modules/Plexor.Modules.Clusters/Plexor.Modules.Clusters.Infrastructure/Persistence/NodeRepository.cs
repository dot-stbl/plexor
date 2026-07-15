// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRepository — per-module subclass of Repository<Node>.
// ==========================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     Read repository for <see cref="Node" />. Write paths stay on
///     <c>ClusterDbContext</c> directly via the command handlers.
/// </summary>
/// <param name="db">The shared <c>forge</c> schema DbContext.</param>
public sealed class NodeRepository(ClusterDbContext db) : Repository<Node>
{
    /// <inheritdoc />
    protected override IQueryable<Node> Query => db.Nodes;
}
