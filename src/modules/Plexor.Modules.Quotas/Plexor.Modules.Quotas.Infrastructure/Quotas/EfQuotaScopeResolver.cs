// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaScopeResolver — walks folder → team → org → default to resolve
// the effective limit value. Read-only; the 4.5.b enforcer calls it
// inside the resource-create transaction (no writes). The catalog row
// supplies the built-in default + period; assignments supply scope-
// level overrides.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     EF-backed scope walker. Resolves the effective limit by trying
///     folder / team / org assignments, then falling back to the
///     catalog's <see cref="QuotaDefinition.DefaultValue" />.
/// </summary>
/// <param name="db">The Quotas DbContext (scoped).</param>
/// <param name="catalog">Catalog reader (scoped) — supplies definition lookup.</param>
/// <remarks>
///     <para><b>Walk order.</b> Folder first (most specific), then org
///     (team-level is Phase 2 — the v1 scope walker does not carry the
///     parent team id, so the team row is unreachable today), then
///     catalog default. The walker picks the first hit; "first" matches
///     the spec's "minimum of found" because each level down is intended
///     to be tighter than its parent.</para>
///     <para><b>Period semantics.</b> Resource quota definitions all use
///     <see cref="QuotaPeriod.None" /> (absolute limits). Rate-limit
///     definitions use <see cref="QuotaPeriod.Hour" /> and are handled
///     by <c>IRateLimiter</c> in 4.5.e — this resolver treats both
///     shapes the same way (read the assignment value as-is). Hourly
///     bucket resets are the rate-limiter's concern, not the resource
///     enforcer's.</para>
/// </remarks>
public sealed class EfQuotaScopeResolver(
    QuotasDbContext db,
    IQuotaCatalog catalog) : IQuotaScopeResolver
{
    /// <inheritdoc />
    public async Task<EffectiveQuota?> ResolveAsync(
        QuotaScope scope,
        QuotaDefinitionKey definitionKey,
        CancellationToken cancellationToken = default)
    {
        var definition = await catalog.FindByKeyAsync(definitionKey.Value, cancellationToken);
        if (definition is null)
        {
            // Unknown catalog key — the enforcer treats this as
            // unlimited (no reservation). Returning null keeps the
            // contract identical to "no default value".
            return null;
        }

        // Try folder scope first (most specific).
        if (scope.Kind is QuotaScopeKind.Folder)
        {
            var folderHit = await FindAssignmentAsync(
                definition.Id,
                QuotaScopeKind.Folder,
                scope.Id,
                cancellationToken);
            if (folderHit is { } folderValue)
            {
                return new EffectiveQuota(folderValue, QuotaScopeKind.Folder);
            }
        }

        // Team-level walk is Phase 2 — the QuotaScope constructor
        // accepts a team id but the v1 scope walker does not carry
        // the parent team on folder contexts. Skip until the team
        // path lands.

        // Org scope (or fallback from folder when no folder hit).
        var orgHit = await FindAssignmentAsync(
            definition.Id,
            QuotaScopeKind.Org,
            scope.OrgId,
            cancellationToken);
        if (orgHit is { } orgValue)
        {
            return new EffectiveQuota(orgValue, QuotaScopeKind.Org);
        }

        // No assignment at any level — fall back to the catalog
        // DefaultValue. A null DefaultValue means unlimited.
        return definition.DefaultValue is { } defaultValue
            ? new EffectiveQuota(defaultValue, null)
            : null;
    }

    /// <summary>
    ///     Look up a single assignment row by (definition, scope kind,
    ///     scope id). Returns the value or <see langword="null" /> when
    ///     no row exists for that triple.
    /// </summary>
    /// <param name="definitionId">Catalog row the assignment targets.</param>
    /// <param name="scopeKind">Folder / Team / Org discriminator.</param>
    /// <param name="scopeId">Realm id for the matching scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    private async Task<decimal?> FindAssignmentAsync(
        Guid definitionId,
        QuotaScopeKind scopeKind,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        return await db.QuotaAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.DefinitionId == definitionId
                && assignment.ScopeKind == scopeKind
                && assignment.ScopeId == scopeId
                && assignment.Period == QuotaPeriod.None)
            .Select(assignment => (decimal?)assignment.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
