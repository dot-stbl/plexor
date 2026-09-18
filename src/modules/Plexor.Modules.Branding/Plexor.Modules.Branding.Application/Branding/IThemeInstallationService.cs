// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IThemeInstallationService — application-layer facade over the
// theme-marketplace persistence boundary. Reads the per-org
// installation row (returns null when no theme has been activated)
// and performs the upsert / delete that powers the marketplace
// PUT / DELETE endpoints.
//
// The verifier + signing path live here rather than in the API
// project so the host invariants ("a manifest is rejected when its
// signature doesn't match") are owned by the same code that owns
// the persistence invariant ("a manifest + id are written
// atomically"). The API project is a thin orchestrator.
// ============================================================================

using Plexor.Modules.Branding.Domain.Entities;

namespace Plexor.Modules.Branding.Application.Branding;

/// <summary>
///     Theme-marketplace persistence facade. Reads the per-org
///     installation row and performs the manifest-verifying upsert
///     / delete that power the marketplace endpoints.
/// </summary>
public interface IThemeInstallationService
{
    /// <summary>
    ///     Read the per-org installation row. Returns
    ///     <see langword="null" /> when no marketplace theme is
    ///     active — the controller maps that to a 404, the boot
    ///     script falls back to the resolved operator defaults.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<ThemeInstallation?> GetForOrgAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Upsert the per-org installation row. Looks up the
    ///     canonical manifest in the host registry, signs it with
    ///     the purpose-bound HMAC verifier, and persists the row.
    ///     An unknown <paramref name="themeId" /> surfaces as
    ///     <see cref="UnknownThemeException" /> (mapped to 404).
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="themeId">Stable marketplace id from the host
    /// registry.</param>
    /// <param name="actorUserId">User that applied the activation
    /// (audit trail).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<ThemeInstallation> UpsertAsync(
        Guid orgId,
        string themeId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete the per-org installation row — the tenant reverts
    ///     to the resolved operator defaults. Idempotent: a missing
    ///     row returns <see langword="false" /> without throwing.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<bool> DeleteAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);
}
