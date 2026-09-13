// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOrgSeeder — EF-backed implementation of IOrgSeeder. Reads the
// quotas.quota_definitions catalog, computes the set of catalog keys
// that have no org-scoped QuotaAssignment row yet, and inserts one
// assignment per missing key with Value = QuotaDefinition.DefaultValue.
//
// Idempotency is two-layered:
//   1. The pre-read step SELECTs existing (definition_id, period)
//      pairs for (ScopeKind = Org, ScopeId = orgId). The set difference
//      against the catalog defines the rows to insert.
//   2. The composite UNIQUE constraint on (definition_id, scope_kind,
//      scope_id, period) backs the read with a hard invariant — even
//      under racing seeders a duplicate INSERT fails and is caught
//      downstream.
//
// The single-row insert path matches the 4.5.b EfQuotaEnforcer
// pattern: scoped DbContext + BeginTransactionIfSupportedAsync so
// InMemory tests skip the transaction.
// ============================================================================

using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     EF-backed <see cref="IOrgSeeder" />. Wraps a scoped
///     <see cref="QuotasDbContext" /> and uses the injected
///     <see cref="TimeProvider" /> for the row's
///     <c>CreatedAt</c> / <c>UpdatedAt</c> stamps.
/// </summary>
/// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the row's stamps.</param>
/// <remarks>
///     <para><b>Why scoped, not singleton.</b> The seeder shares the
///     caller's per-request DbContext — same lifetime pattern as the
///     enforcer. The hosted-service caller opens its own scope per
///     sweep (see <c>OrgSeederHostedService</c>).</para>
///     <para><b>Why a transaction wrapper.</b> On Postgres the single
///     <c>SaveChangesAsync</c> call already commits atomically; the
///     transaction is a no-op there but lets the same code path run
///     against the InMemory provider (which does not support
///     transactions) without a runtime exception.</para>
/// </remarks>
public sealed class EfOrgSeeder(QuotasDbContext db, TimeProvider clock) : IOrgSeeder
{
    /// <inheritdoc />
    public async Task<int> SeedOrgAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await EfOrgSeederHelpers.SeedOrgInternalAsync(db, clock, orgId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> SeedAllOrgsAsync(
        IReadOnlyCollection<Guid> orgIds,
        CancellationToken cancellationToken = default)
    {
        var total = 0;
        foreach (var orgId in orgIds)
        {
            total += await EfOrgSeederHelpers.SeedOrgInternalAsync(db, clock, orgId, cancellationToken);
        }

        return total;
    }
}