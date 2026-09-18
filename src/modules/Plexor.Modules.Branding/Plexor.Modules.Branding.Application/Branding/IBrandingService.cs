// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IBrandingService — application-layer facade over the Branding
// persistence boundary. Combines the global config + per-org
// override into the resolved boot config the frontend consumes.
//
// Reads/writes happen behind an EfBrandingRepository implementation;
// the controller depends on this interface, not on the DbContext.
// ============================================================================

using Plexor.Modules.Branding.Domain.Entities;

namespace Plexor.Modules.Branding.Application.Branding;

/// <summary>
///     Application-layer service for the branding capability. Reads +
///     writes the operator global row and the per-org override row;
///     resolves the merged boot config the frontend consumes at
///     boot time.
/// </summary>
public interface IBrandingService
{
    /// <summary>
    ///     Read the singleton operator-global branding row. Returns
    ///     a sentinel default when the row doesn't exist yet (first
    ///     boot before the seeder ran) — the controller maps that to
    ///     a 200 with the defaults rather than a 404.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<GlobalThemeConfig> GetGlobalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Upsert the singleton operator-global branding row. The
    ///     seeder inserts a default row on first boot; subsequent
    ///     calls update it in place.
    /// </summary>
    /// <param name="config">The new values.</param>
    /// <param name="actorUserId">Id of the operator user making the change (audit trail).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<GlobalThemeConfig> UpsertGlobalAsync(
        GlobalThemeConfig config,
        Guid? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Read the per-org override row. Returns <see langword="null" />
    ///     when the org has no override — callers should fall back to
    ///     <see cref="GetGlobalAsync" />.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<OrgThemeConfig?> GetOrgOverrideAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Upsert the per-org override row. Inserts when no row
    ///     exists for the org; updates in place when one does.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="config">Override values (null fields fall back to global).</param>
    /// <param name="actorUserId">Id of the operator user making the change (audit trail).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<OrgThemeConfig> UpsertOrgOverrideAsync(
        Guid orgId,
        OrgThemeConfig config,
        Guid? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete the per-org override row — the tenant fully
    ///     reverts to the operator defaults. Idempotent: a missing
    ///     row is a no-op (returns <see langword="false" />).
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<bool> DeleteOrgOverrideAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolve the boot config for one tenant: read the global
    ///     row, optionally read the org override, merge (org wins
    ///     on every non-null field), return the result. The host
    ///     calls this from the <c>GET /api/v1/branding/boot</c>
    ///     endpoint that the boot-config script consumes.
    /// </summary>
    /// <param name="orgId">Optional tenant scope. Null = anonymous
    /// / no tenant — resolve to the operator defaults only.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<ResolvedBrandingConfig> ResolveForOrgAsync(
        Guid? orgId,
        CancellationToken cancellationToken = default);
}