// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRecordRepository — per-module subclass of Repository<NodeRecord>.
//
// Read surface for NodeRecord; the write paths (Register / Heartbeat)
// stay on OutpostDbContext directly in the command handlers so they
// can compose multi-entity updates (token revoke + node insert on
// join; node update + workload reconciliation on heartbeat).
// ============================================================================

using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Infrastructure.Persistence;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Outpost.Infrastructure.Persistence.Repositories;

/// <summary>
///     Read repository for <see cref="NodeRecord" />. Write paths stay
///     on <c>OutpostDbContext</c> directly via the command handlers.
/// </summary>
/// <param name="db">The shared <c>outpost</c> schema DbContext.</param>
public sealed class NodeRecordRepository(OutpostDbContext db) : Repository<NodeRecord>
{
    /// <inheritdoc />
    protected override IQueryable<NodeRecord> Query => db.NodeRecords;
}