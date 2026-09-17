// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfStorageQuotaReader — Phase 4.5.d EF Core implementation of
// IStorageQuotaReader. Read-only lookups against storage.volumes.
// AsNoTracking + a single GROUP BY aggregate (COUNT(*) + SUM(size_gb))
// scoped to (org_id) — the quota enforcer calls this on every
// CreateVolume path.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Storage.Application.Storage;
using Plexor.Modules.Storage.Domain.Projections;
using Plexor.Modules.Storage.Infrastructure.Persistence;

namespace Plexor.Modules.Storage.Infrastructure.Installers;

/// <summary>
///     EF Core implementation of <see cref="IStorageQuotaReader" />.
///     Uses <see cref="StorageDbContext" /> with <c>AsNoTracking()</c>
///     reads — no change tracker involvement, no per-row state.
/// </summary>
/// <param name="db">Scoped <see cref="StorageDbContext" />.</param>
internal sealed class EfStorageQuotaReader(StorageDbContext db) : IStorageQuotaReader
{
    /// <inheritdoc />
    public async Task<StorageOrgScopedCounters> CountAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        // Aggregate in a single round-trip — COUNT + SUM over the
        // org-scoped subset of storage.volumes. The (org_id) index
        // backs the WHERE filter. SUM on an empty set returns null in
        // Postgres; coalesce to 0m so callers get a numeric total.
        var aggregate = await db.Volumes
            .AsNoTracking()
            .Where(volume => volume.OrgId == orgId)
            .GroupBy(static volume => 1)
            .Select(static grouping => new
            {
                Count = grouping.Count(),
                GbTotal = grouping.Sum(static volume => (decimal)volume.SizeGb),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return aggregate is null
            ? new StorageOrgScopedCounters(VolumeCount: 0, VolumeGbTotal: 0m)
            : new StorageOrgScopedCounters(
                VolumeCount: aggregate.Count,
                VolumeGbTotal: aggregate.GbTotal);
    }
}
