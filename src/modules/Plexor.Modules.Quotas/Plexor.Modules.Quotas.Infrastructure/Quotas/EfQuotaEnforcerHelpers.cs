// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaEnforcerHelpers — file-static helpers pulled out of
// EfQuotaEnforcer.cs to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a). The three methods here are:
//   * UpsertAndReadCurrentAsync — atomic INSERT ... ON CONFLICT DO UPDATE
//     RETURNING that the enforcer's step 3 needs to know the current_value.
//   * ComputeLockKey — FNV-1a 64-bit hash over the scope seed, used as the
//     pg_advisory_xact_lock key (step 2).
//   * ScopeKindString — lowercase enum name for the Postgres varchar(16)
//     column; centralised so the wire form stays identical across the
//     upsert, the UPDATE, and the lock-key seed.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Helpers for <see cref="EfQuotaEnforcer" /> — pulled out so the
///     enforcer stays free of private methods (class-layout-and-tooling
///     §1a).
/// </summary>
internal static class EfQuotaEnforcerHelpers
{
    /// <summary>
    ///     Upsert the <c>quota_usage</c> row and read the resulting
    ///     <c>current_value</c>. The <c>ON CONFLICT DO UPDATE SET
    ///     current_value = current_value</c> shape is a no-op on
    ///     existing rows — the only purpose is to guarantee the row
    ///     exists before the subsequent UPDATE.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" /> the caller's transaction is on.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the row's stamps.</param>
    /// <param name="scope">Polymorphic scope the row targets.</param>
    /// <param name="definitionId">Catalog row id the snapshot measures.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task<decimal> UpsertAndReadCurrentAsync(
        QuotasDbContext db,
        TimeProvider clock,
        QuotaScope scope,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var values = await db.Database
            .SqlQueryRaw<decimal>(
                @"INSERT INTO quotas.quota_usage
                    (scope_kind, scope_id, definition_id, org_id,
                     current_value, period_start, last_reconciled_at,
                     created_at, updated_at)
                  VALUES ({0}, {1}, {2}, {3}, 0, {4}, {4}, {4}, {4})
                  ON CONFLICT (scope_kind, scope_id, definition_id) DO UPDATE
                  SET current_value = quotas.quota_usage.current_value
                  RETURNING current_value",
                ScopeKindString(scope.Kind),
                scope.Id,
                definitionId,
                scope.OrgId,
                now)
            .ToListAsync(cancellationToken);

        return values.Single();
    }

    /// <summary>
    ///     Stable, scope-keyed 64-bit hash for the advisory lock. Uses
    ///     FNV-1a 64-bit — same input always produces the same lock
    ///     key across hosts, no salt needed.
    /// </summary>
    /// <param name="scope">Scope the lock protects.</param>
    public static long ComputeLockKey(QuotaScope scope)
    {
        const ulong FnvOffsetBasis = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        var seed = $"quota:{ScopeKindString(scope.Kind)}:{scope.Id:N}";
        var hash = FnvOffsetBasis;
        unchecked
        {
            foreach (var character in seed)
            {
                hash ^= character;
                hash *= FnvPrime;
            }
        }

        return (long)hash;
    }

    /// <summary>
    ///     Lowercase enum name for the Postgres <c>varchar(16)</c>
    ///     column. Centralised so the wire form stays identical across
    ///     the upsert, the UPDATE, and the lock-key seed.
    /// </summary>
    /// <param name="kind">The scope kind to render.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="kind" /> is not a known <see cref="QuotaScopeKind" /> value.
    /// </exception>
    public static string ScopeKindString(QuotaScopeKind kind)
    {
        return kind switch
        {
            QuotaScopeKind.Org => "org",
            QuotaScopeKind.Team => "team",
            QuotaScopeKind.Folder => "folder",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown QuotaScopeKind"),
        };
    }
}