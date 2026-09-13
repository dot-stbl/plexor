// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaCatalog — EF-backed implementation of IQuotaCatalog. Reads the
// quotas.quota_definitions table; consumed by the 4.5.b scope resolver
// (to look up DefaultValue when no assignment exists for a scope) and
// the 4.5.g catalog endpoint (to surface the catalog to operators).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Read-only <see cref="IQuotaCatalog" /> implementation. Wraps a
///     scoped <see cref="QuotasDbContext" />; the resolver is the only
///     v1 caller, so the surface stays narrow.
/// </summary>
/// <param name="db">The Quotas DbContext (scoped).</param>
public sealed class EfQuotaCatalog(QuotasDbContext db) : IQuotaCatalog
{
    /// <inheritdoc />
    public async Task<QuotaDefinition?> FindByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await db.QuotaDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                definition => definition.Key == key,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuotaDefinition>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.QuotaDefinitions
            .AsNoTracking()
            .OrderBy(static definition => definition.Key)
            .ToArrayAsync(cancellationToken);
    }
}
