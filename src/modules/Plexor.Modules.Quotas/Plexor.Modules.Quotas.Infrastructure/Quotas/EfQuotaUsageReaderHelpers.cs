// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaUsageReaderHelpers — file-static helpers pulled out of
// EfQuotaUsageReader.cs to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a / §9.1). The reader is a thin
// façade; the actual query lives here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Helpers for <see cref="EfQuotaUsageReader" />. One method today;
///     the per-method split is purely to honour the convention.
/// </summary>
internal static class EfQuotaUsageReaderHelpers
{
    /// <summary>
    ///     Read all usage rows for one (scope, org). Filters at the
    ///     query level — callers in Org X never see Org Y's
    ///     snapshots.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="scopeKind">Org / Team / Folder discriminator.</param>
    /// <param name="scopeId">Id of the matching Realm entity.</param>
    /// <param name="orgId">Tenant boundary.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The matching rows ordered by definition id (stable
    /// ordering keeps the dashboard's row order deterministic).</returns>
    public static async Task<IReadOnlyList<QuotaUsage>> ListForScopeInternalAsync(
        QuotasDbContext db,
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        return await db.QuotaUsage
            .AsNoTracking()
            .Where(usage =>
                usage.ScopeKind == scopeKind
                && usage.ScopeId == scopeId
                && usage.OrgId == orgId)
            .OrderBy(static usage => usage.DefinitionId)
            .ToListAsync(cancellationToken);
    }
}
