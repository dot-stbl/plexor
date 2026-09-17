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
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Modules.Storage.Application.Storage;
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
/// <param name="auditEmitter">Scoped <see cref="IQuotaAuditEmitter" /> — emits <c>UsageExceeded</c> + <c>LimitApproaching</c> events at the audit points (4.5.h).</param>
/// <param name="storageQuotaReader">Scoped <see cref="IStorageQuotaReader" /> — supplies the current org-scoped volume count + GiB for the storage.volumes.* keys (4.5.d).</param>
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
///     <para><b>Cross-module seam (4.5.d).</b>
///     <c>storage.volumes.count</c> and <c>storage.volumes.gb</c> keys
///     depend on rows owned by <c>Plexor.Modules.Storage</c>. Rather
///     than injecting <c>StorageDbContext</c> directly into the enforcer
///     (which would cross Law 3 — modules don't reference each other's
///     Infrastructure), the Quotas module defines a
///     <see cref="IStorageQuotaReader" /> seam in
///     <c>Plexor.Modules.Storage.Application</c>; the Storage module's
///     own Infrastructure installer binds it to
///     <c>EfStorageQuotaReader</c>. Mirrors
///     <c>IOrgAuthProviderConfigReader</c> (Realm → Sigil).</para>
///     <para><b>Audit emission (4.5.h).</b> The enforcer emits
///     <c>UsageExceeded</c> on a <c>Denied</c> result and
///     <c>LimitApproaching</c> on a successful reservation that crossed
///     the 80% threshold. Both calls happen inline (the structured-log
///     implementation is microseconds; a future DB-backed emitter can
///     swap to a channel if the I/O cost becomes significant). The
///     emitter MUST NOT throw — the audit contract is fire-and-forget
///     so a quota denial never breaks the user request.</para>
/// </remarks>
internal sealed class EfQuotaEnforcer(
    QuotasDbContext db,
    IQuotaScopeResolver resolver,
    IQuotaCatalog catalog,
    TimeProvider clock,
    IQuotaAuditEmitter auditEmitter,
    IStorageQuotaReader storageQuotaReader) : IQuotaEnforcer
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
        var lockKey = EfQuotaEnforcerHelpers.ComputeLockKey(scope);
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

        var currentValue = await EfQuotaEnforcerHelpers.UpsertAndReadCurrentAsync(
            db,
            clock,
            scope,
            definition.Id,
            cancellationToken);

        // Step 4 — decide. proposed = current + amount. If proposed
        // exceeds the effective limit, the caller rolls back the
        // transaction (Denied is returned, no UPDATE).
        var proposed = currentValue + amount;

        // Step 4b (4.5.d) — storage keys have an *external* source of
        // truth (the physical count + sum of storage.volumes rows).
        // The snapshot path above would drift on volume delete (the
        // snapshot grows monotonically, the physical count doesn't);
        // for storage keys we bypass the snapshot entirely and gate
        // the request on the live aggregate from
        // IStorageQuotaReader. When the storage check passes we also
        // skip the snapshot UPDATE (no need to track cumulative
        // reservations — the live aggregate IS the current state).
        if (await IsStorageKeyAsync(definitionKey.Value, cancellationToken))
        {
            if (await IsStorageLimitViolatedAsync(
                    scope,
                    definitionKey.Value,
                    amount,
                    cancellationToken)
                is { } storageViolation)
            {
                await auditEmitter.EmitAsync(
                    QuotaAuditEvent.UsageExceeded,
                    new QuotaAuditContext(
                        OrgId: scope.OrgId,
                        ActorUserId: scope.ActorUserId,
                        DefinitionKey: definitionKey.Value,
                        ScopeKind: scope.Kind.ToString(),
                        ScopeId: scope.Id,
                        Used: currentValue,
                        Limit: effective.Value,
                        Requested: amount),
                    cancellationToken);

                return new QuotaCheckResult.Denied(
                    Limit: effective.Value,
                    Used: currentValue,
                    Requested: amount,
                    Reason: storageViolation);
            }

            return new QuotaCheckResult.Allowed();
        }

        if (proposed > effective.Value)
        {
            await auditEmitter.EmitAsync(
                QuotaAuditEvent.UsageExceeded,
                new QuotaAuditContext(
                    OrgId: scope.OrgId,
                    ActorUserId: scope.ActorUserId,
                    DefinitionKey: definitionKey.Value,
                    ScopeKind: scope.Kind.ToString(),
                    ScopeId: scope.Id,
                    Used: currentValue,
                    Limit: effective.Value,
                    Requested: amount),
                cancellationToken);

            return new QuotaCheckResult.Denied(
                Limit: effective.Value,
                Used: currentValue,
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
            EfQuotaEnforcerHelpers.ScopeKindString(scope.Kind),
            scope.Id,
            definition.Id);

        if (crossesWarning)
        {
            await auditEmitter.EmitAsync(
                QuotaAuditEvent.LimitApproaching,
                new QuotaAuditContext(
                    OrgId: scope.OrgId,
                    ActorUserId: scope.ActorUserId,
                    DefinitionKey: definitionKey.Value,
                    ScopeKind: scope.Kind.ToString(),
                    ScopeId: scope.Id,
                    Used: proposed,
                    Limit: effective.Value,
                    Requested: amount,
                    ThresholdPct: WarningThresholdPct),
                cancellationToken);
        }

        return crossesWarning
            ? new QuotaCheckResult.AllowedWithWarning(
                ThresholdPct: WarningThresholdPct,
                Used: proposed,
                Limit: effective.Value)
            : new QuotaCheckResult.Allowed();
    }

    /// <summary>
    ///     Return true when the catalog key is one of the storage
    ///     limits that needs a cross-module aggregate read against
    ///     Plexor.Modules.Storage. Two keys ship in 4.5.d:
    ///     <c>storage.volumes.count</c> + <c>storage.volumes.gb</c>.
    ///     The check is by definition key — the storage module's
    ///     catalogue is owned there, the enforcer here just dispatches
    ///     on the wire name.
    /// </summary>
    private static async Task<bool> IsStorageKeyAsync(
        string definitionKey,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return definitionKey is "storage.volumes.count" or "storage.volumes.gb";
    }

    /// <summary>
    ///     Read the current org-scoped volume count + cumulative GiB
    ///     from <see cref="IStorageQuotaReader" />, then project the
    ///     proposed post-reservation state and compare against the
    ///     effective limit. Returns null when both checks fit (caller
    ///     proceeds); returns a denial reason when either count or
    ///     GiB would exceed the limit.
    /// </summary>
    /// <remarks>
    ///     <para><b>Why a separate aggregate read.</b> The
    ///     quotas.quota_usage snapshot reflects cumulative reservations
    ///     across all historical calls. For storage keys the truth
    ///     is the *physical* count of rows in storage.volumes — a
    ///     misuse (e.g. caller passes 0 amount to bypass the count
    ///     check, or two enforcer paths race) cannot inflate the
    ///     snapshot while still creating the row. The check uses
    ///     <see cref="Plexor.Modules.Storage.Domain.Projections.StorageOrgScopedCounters" />
    ///     as the source of truth and adds the new amount in-flight.</para>
    ///     <para><b>Org scope only in v0.1.</b> Folder / team scopes are
    ///     Phase 2 — the reader takes orgId and returns a single
    ///     aggregate. A future folder-scoped quota would require the
    ///     reader to take a FolderId + OrgId pair and the count query
    ///     to add a <c>folder_id = ?</c> predicate.</para>
    /// </remarks>
    private async Task<string?> IsStorageLimitViolatedAsync(
        QuotaScope scope,
        string definitionKey,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var aggregate = await storageQuotaReader.CountAsync(scope.OrgId, cancellationToken);
        var definitionLimit = await ResolveStorageLimitAsync(scope, definitionKey, cancellationToken);

        return definitionKey switch
        {
            "storage.volumes.count" => VolumeCountWouldExceed(aggregate, amount, definitionLimit),
            "storage.volumes.gb" => VolumeGbWouldExceed(aggregate, amount, definitionLimit),
            _ => null,
        };
    }

    /// <summary>
    ///     Fetch the same effective limit the snapshot path uses
    ///     (resolved through the catalog + assignment walker) so the
    ///     storage denial reason reports the limit callers saw on the
    ///     catalog endpoint. Pure helper; no side effects.
    /// </summary>
    private async Task<decimal> ResolveStorageLimitAsync(
        QuotaScope scope,
        string definitionKey,
        CancellationToken cancellationToken)
    {
        _ = await catalog.FindByKeyAsync(definitionKey, cancellationToken)
            ?? throw new InvalidOperationException(
                $"QuotaDefinition '{definitionKey}' resolved to a null row — catalog state is inconsistent.");
        var resolved = await resolver.ResolveAsync(scope, new QuotaDefinitionKey(definitionKey), cancellationToken);
        return resolved?.Value ?? 0m;
    }

    private static string? VolumeCountWouldExceed(
        Plexor.Modules.Storage.Domain.Projections.StorageOrgScopedCounters aggregate,
        decimal amount,
        decimal limit)
    {
        var totalAfter = aggregate.VolumeCount + (long)amount;
        return totalAfter > limit
            ? $"storage.volumes.count would exceed {limit} (current {aggregate.VolumeCount}, requested {amount})"
            : null;
    }

    private static string? VolumeGbWouldExceed(
        Plexor.Modules.Storage.Domain.Projections.StorageOrgScopedCounters aggregate,
        decimal amount,
        decimal limit)
    {
        var totalAfter = aggregate.VolumeGbTotal + amount;
        return totalAfter > limit
            ? $"storage.volumes.gb would exceed {limit} GiB (current {aggregate.VolumeGbTotal}, requested {amount})"
            : null;
    }
}
