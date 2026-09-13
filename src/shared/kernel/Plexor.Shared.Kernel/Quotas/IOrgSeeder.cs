// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOrgSeeder — seeds default QuotaAssignment rows for one or more orgs.
// Idempotent: re-running on an already-seeded org is a no-op for the keys
// already present. The seeded rows surface every catalog default to the
// admin UI (4.5.g) and give the enforcer a row to UPDATE on operator
// raise, while the resolver still falls back to QuotaDefinition.DefaultValue
// if the row is missing (so the seeder is correctness-neutral for v0.1).
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Seeds default <c>quota_assignments</c> rows for one or more
///     organizations. Re-runs are no-ops for keys already present.
/// </summary>
/// <remarks>
///     <para><b>Why a shared-kernel interface.</b> The composition
///     root (Plexor.Host / Plexor.Migrator) wires the
///     <see cref="IOrgSeeder" /> through the Quotas Application installer
///     — the seam exists so the host does not depend on the Quotas
///     Infrastructure assembly directly when the hosted service resolves
///     org ids.</para>
///     <para><b>Why org scope only.</b> v1 ships org-scope defaults. Team
///     and folder scopes are created lazily by the operator via 4.5.g
///     (<c>PUT /api/v1/quotas/assignments</c>). Phase 2 may extend the
///     seeder with a per-team loop — the contract already supports it
///     (per-org id is the only input).</para>
///     <para><b>What "seeded" means.</b> For each <c>QuotaDefinition</c>
///     row with a non-null <c>DefaultValue</c>, one
///     <c>QuotaAssignment</c> row is created at the org scope
///     (<c>ScopeKind = Org</c>, <c>ScopeId = orgId</c>,
///     <c>OrgId = orgId</c>) with <c>Value = DefaultValue</c>,
///     <c>Period</c> copied from the definition, and
///     <c>CreatedBy = Guid.Empty</c> (the empty GUID marks the row as
///     system-seeded; the column is non-nullable by design).</para>
/// </remarks>
public interface IOrgSeeder
{
    /// <summary>
    ///     Seed every catalog default for one organization.
    /// </summary>
    /// <param name="orgId">Organization to seed.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    ///     The number of rows inserted. <c>0</c> when the org already
    ///     has an assignment for every catalog key.
    /// </returns>
    public Task<int> SeedOrgAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Seed every catalog default for every organization in the
    ///     supplied collection. Used by the startup hosted service in
    ///     <c>Plexor.Host</c> and <c>Plexor.Migrator</c>.
    /// </summary>
    /// <param name="orgIds">Organizations to seed.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    ///     The total number of rows inserted across all orgs.
    /// </returns>
    public Task<int> SeedAllOrgsAsync(
        IReadOnlyCollection<Guid> orgIds,
        CancellationToken cancellationToken = default);
}