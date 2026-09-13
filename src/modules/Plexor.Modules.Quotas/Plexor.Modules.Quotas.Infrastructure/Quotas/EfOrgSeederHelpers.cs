// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOrgSeederHelpers — file-static helpers pulled out of EfOrgSeeder.cs
// to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a / §9.1 — Repository/port implementation).
// EfOrgSeeder is a thin façade; the actual seed routine lives here so the
// façade stays one-method-per-interface-method.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Helpers for <see cref="EfOrgSeeder" />. Pulled out so the
///     façade stays free of private methods (class-layout-and-tooling
///     §1a / §9.1).
/// </summary>
internal static class EfOrgSeederHelpers
{
    /// <summary>
    ///     Core single-org seed routine. Reads the catalog, computes the
    ///     set difference against the existing org-scoped assignments,
    ///     and bulk-inserts the missing rows in one SaveChanges call.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the row stamps.</param>
    /// <param name="orgId">Organization to seed.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>Number of rows inserted (0 when the org is already fully seeded).</returns>
    /// <remarks>
    ///     <para><b>Skip rule.</b> Catalog rows with
    ///     <c>DefaultValue = null</c> are skipped — "unlimited" is the
    ///     correct resolution for a key with no operator-chosen default,
    ///     and seeding a row with a NULL value would defeat the purpose
    ///     (admin would have nothing to inspect or update).</para>
    ///     <para><b>Existing-key probe.</b> Keys are compared by
    ///     <c>(DefinitionId, Period)</c> — the same composite the UNIQUE
    ///     constraint enforces. v1 catalog has unique combinations so a
    ///     definition id alone is sufficient today; the pair form is
    ///     future-safe against a same-key-different-period split.</para>
    ///     <para><b>CreatedBy = Guid.Empty.</b> The column is non-nullable;
    ///     the empty GUID is the v1 marker for "system-seeded". The
    ///     4.5.g PUT path writes the real actor id when an admin edits
    ///     the row.</para>
    /// </remarks>
    public static async Task<int> SeedOrgInternalAsync(
        QuotasDbContext db,
        TimeProvider clock,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        var definitions = await db.QuotaDefinitions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (definitions.Count == 0)
        {
            return 0;
        }

        var existingKeys = await db.QuotaAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.ScopeKind == QuotaScopeKind.Org
                && assignment.ScopeId == orgId)
            .Select(assignment => new { assignment.DefinitionId, assignment.Period })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys
            .Select(key => (key.DefinitionId, key.Period))
            .ToHashSet();

        var now = clock.GetUtcNow();
        var inserted = 0;

        await using var transaction = await db.Database
            .BeginTransactionIfSupportedAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            if (!definition.DefaultValue.HasValue)
            {
                continue;
            }

            if (existingSet.Contains((definition.Id, definition.Period)))
            {
                continue;
            }

            await db.QuotaAssignments.AddAsync(new QuotaAssignment
            {
                Id = Guid.NewGuid(),
                DefinitionId = definition.Id,
                ScopeKind = QuotaScopeKind.Org,
                ScopeId = orgId,
                OrgId = orgId,
                Value = definition.DefaultValue.Value,
                Period = definition.Period,
                CreatedBy = Guid.Empty,
                CreatedAt = now,
                UpdatedAt = now,
            }, cancellationToken);

            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return inserted;
    }
}