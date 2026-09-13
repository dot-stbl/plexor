// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaEnforcer — the atomic check-and-reserve path. Acquires a
// Postgres pg_advisory_xact_lock at scope granularity, upserts the
// quota_usage row, decides (Allowed / AllowedWithWarning / Denied), and
// increments current_value when the call fits.
//
// Must run inside the same DB transaction as the caller's resource
// INSERT — the advisory lock is released by Postgres at COMMIT/
// ROLLBACK so a failed resource create automatically releases the
// reservation. The caller is responsible for opening the transaction.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     EF-backed <see cref="IQuotaEnforcer" />. Atomic check + reserve
///     with serialisation via <c>pg_advisory_xact_lock</c> at scope
///     granularity.
/// </summary>
/// <param name="db">Scoped <see cref="QuotasDbContext" /> the caller's transaction is on.</param>
/// <param name="resolver">Scoped <see cref="IQuotaScopeResolver" /> for the effective limit.</param>
/// <param name="catalog">Scoped <see cref="IQuotaCatalog" /> for definition lookup.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's <c>LastReconciledAt</c> / <c>UpdatedAt</c> stamps.</param>
/// <param name="logger">Structured logger for denied + warning events.</param>
/// <remarks>
///     <para><b>Lock key.</b> A stable 64-bit hash of
///     <c>$"quota:{scope.Kind}:{scope.Id}"</c>. Two callers for the same
///     (Kind, Id) hash to the same <see cref="long" /> and serialise on
///     the advisory lock. Different scopes hash differently and proceed
///     in parallel.</para>
///     <para><b>No transaction here.</b> The enforcer participates in
///     the caller's ambient transaction via the shared DbContext. If the
///     caller rolls back, the reservation is gone — that's the contract.</para>
///     <para><b>Why raw SQL for the upsert.</b> The atomic
///     <c>INSERT ... ON CONFLICT ... DO UPDATE RETURNING</c> shape is
///     the simplest way to ensure the row exists with its current value
///     without a separate "ensure exists" round-trip. EF Core's
///     <c>Database.SqlQueryRaw&lt;T&gt;</c> is the canonical escape hatch
///     for scalar projections not bound to a tracked entity.</para>
/// </remarks>
internal sealed class EfQuotaEnforcer(
    QuotasDbContext db,
    IQuotaScopeResolver resolver,
    IQuotaCatalog catalog,
    TimeProvider clock,
    ILogger<EfQuotaEnforcer> logger) : IQuotaEnforcer
{
    /// <summary>80% threshold fires the warning variant of the result.</summary>
    private const decimal WarningThresholdPct = 80m;

    /// <inheritdoc />
    public async Task<QuotaCheckResult> CheckAndReserveAsync(
        QuotaScope scope,
        QuotaDefinitionKey definitionKey,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        // Step 1 — resolve the effective limit. Null means "unlimited"
        // (no catalog row + no assignment at any level).
        var effective = await resolver.ResolveAsync(scope, definitionKey, cancellationToken);
        if (effective is null)
        {
            return new QuotaCheckResult.Allowed();
        }

        // Step 2 — take the per-scope advisory transaction lock so
        // concurrent creates for the same scope serialise. Released
        // automatically at COMMIT / ROLLBACK.
        var lockKey = ComputeLockKey(scope);
        await db.Database
            .ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0})",
                lockKey,
                cancellationToken);

        // Step 3 — ensure the usage row exists, then read the current
        // value. The INSERT ... ON CONFLICT DO UPDATE ... RETURNING
        // shape creates the row if missing or no-ops the existing one
        // (current_value stays put).
        var definition = await catalog.FindByKeyAsync(definitionKey.Value, cancellationToken)
            ?? throw new InvalidOperationException(
                $"QuotaDefinition '{definitionKey.Value}' resolved to a null row — catalog state is inconsistent.");

        var currentValue = await UpsertAndReadCurrentAsync(
            scope,
            definition.Id,
            cancellationToken);

        // Step 4 — decide. proposed = current + amount. If proposed
        // exceeds the effective limit, the caller rolls back the
        // transaction (Denied is returned, no UPDATE).
        var proposed = currentValue + amount;
        if (proposed > effective.Value)
        {
            logger.LogWarning(
                "Quota denied for {ScopeKind}/{ScopeId} on {DefinitionKey}: would be {Proposed}, limit is {Limit}, requested {Amount}",
                scope.Kind,
                scope.Id,
                definitionKey.Value,
                proposed,
                effective.Value,
                amount);

            return new QuotaCheckResult.Denied(
                Limit: effective.Value,
                Requested: amount,
                Reason: $"scope {scope.Kind}/{scope.Id} would exceed {definitionKey.Value} limit ({effective.Value})");
        }

        // Step 5 — update current_value. Advisory lock serialises
        // concurrent writers, so the read-modify-write is safe.
        var warnThreshold = effective.Value * (WarningThresholdPct / 100m);
        var crossesWarning = proposed > warnThreshold;

        await db.Database.ExecuteSqlRawAsync(
            @"UPDATE quotas.quota_usage
              SET current_value = current_value + {0},
                  last_reconciled_at = {1},
                  updated_at = {1}
              WHERE scope_kind = {2}
                AND scope_id = {3}
                AND definition_id = {4}",
            amount,
            clock.GetUtcNow(),
            ScopeKindString(scope.Kind),
            scope.Id,
            definition.Id);

        return crossesWarning
            ? new QuotaCheckResult.AllowedWithWarning(
                ThresholdPct: WarningThresholdPct,
                Used: proposed,
                Limit: effective.Value)
            : new QuotaCheckResult.Allowed();
    }

    /// <summary>
    ///     Upsert the <c>quota_usage</c> row and read the resulting
    ///     <c>current_value</c>. The <c>ON CONFLICT DO UPDATE SET
    ///     current_value = current_value</c> shape is a no-op on
    ///     existing rows — the only purpose is to guarantee the row
    ///     exists before the subsequent UPDATE.
    /// </summary>
    /// <param name="scope">Polymorphic scope the row targets.</param>
    /// <param name="definitionId">Catalog row id the snapshot measures.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    private async Task<decimal> UpsertAndReadCurrentAsync(
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
    private static long ComputeLockKey(QuotaScope scope)
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
    private static string ScopeKindString(QuotaScopeKind kind)
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
