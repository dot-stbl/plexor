// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfBrandingService — EF-backed IBrandingService. Wraps the scoped
// BrandingDbContext and exposes the merged branding snapshot the
// controller resolves for the boot-config script.
//
// The single-row upserts use a SELECT-then-INSERT-or-UPDATE dance
// that runs inside the scoped DbContext lifetime. Concurrent writers
// are safe because:
//   - global_theme_config has a UNIQUE partial index on id (singleton).
//   - org_theme_config has a UNIQUE index on org_id.
// A racing writer hits a unique-violation and the second
// SaveChanges propagates as a 500; callers retry.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Persistence;

namespace Plexor.Modules.Branding.Infrastructure.Branding;

/// <summary>
///     EF-backed implementation of <see cref="IBrandingService" />.
///     Scoped — shares the per-request DbContext lifetime with the
///     controller the action handler wraps. Read-only paths use
///     <c>.AsNoTracking()</c>; write paths rely on the change
///     tracker.
/// </summary>
/// <param name="db">Scoped <see cref="BrandingDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// <c>UpdatedAt</c> / <c>CreatedAt</c> stamps.</param>
public sealed class EfBrandingService(BrandingDbContext db, TimeProvider clock)
    : IBrandingService
{
    /// <inheritdoc />
    public async Task<GlobalThemeConfig> GetGlobalAsync(
        CancellationToken cancellationToken = default)
    {
        return await EfBrandingServiceHelpers.GetGlobalInternalAsync(
            db, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GlobalThemeConfig> UpsertGlobalAsync(
        GlobalThemeConfig config,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        return await EfBrandingServiceHelpers.UpsertGlobalInternalAsync(
            db, clock, config, actorUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrgThemeConfig?> GetOrgOverrideAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await EfBrandingServiceHelpers.GetOrgOverrideInternalAsync(
            db, orgId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrgThemeConfig> UpsertOrgOverrideAsync(
        Guid orgId,
        OrgThemeConfig config,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        return await EfBrandingServiceHelpers.UpsertOrgOverrideInternalAsync(
            db, clock, orgId, config, actorUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteOrgOverrideAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await EfBrandingServiceHelpers.DeleteOrgOverrideInternalAsync(
            db, orgId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ResolvedBrandingConfig> ResolveForOrgAsync(
        Guid? orgId,
        CancellationToken cancellationToken = default)
    {
        return await EfBrandingServiceHelpers.ResolveForOrgInternalAsync(
            db, orgId, cancellationToken);
    }
}