// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfThemeInstallationService — EF-backed IThemeInstallationService.
// Wraps the scoped BrandingDbContext + the purpose-bound
// IThemeManifestVerifier and applies the (verify → upsert) dance
// in the controller's hot path. Per-method helpers live in the
// sibling EfThemeInstallationServiceHelpers file so the service
// class stays a thin orchestrator (no-private-methods
// convention, class-layout-and-tooling.md §1a).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Persistence;
using Plexor.Modules.Branding.Infrastructure.ThemeManifests;
using PlexorThemeManifest = Plexor.Modules.Branding.Application.Branding.ThemeManifest;

namespace Plexor.Modules.Branding.Infrastructure.Branding;

/// <summary>
///     EF-backed implementation of <see cref="IThemeInstallationService" />.
///     Scoped — shares the per-request DbContext lifetime with the
///     controller the action handler wraps. Read paths use
///     <c>.AsNoTracking()</c>; write paths rely on the
///     EF change tracker after the verifier has approved the
///     manifest.
/// </summary>
/// <param name="db">Scoped <see cref="Plexor.Modules.Branding.Infrastructure.Persistence.BrandingDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for
/// the <c>ActivatedAt</c> stamp.</param>
/// <param name="verifier">HMAC verifier that gates the upsert —
/// an invalid signature short-circuits before <c>SaveChanges</c>.</param>
/// <param name="registry">Hardcoded community-theme list that
/// maps a <c>themeId</c> to its canonical metadata. Mirrors the
/// FE bundle in <c>web/apps/console/src/shared/lib/themes/community-themes.ts</c>
/// so the host doesn't have to trust the FE bundle to know what
/// themes exist.</param>
public sealed class EfThemeInstallationService(
    BrandingDbContext db,
    TimeProvider clock,
    IThemeManifestVerifier verifier,
    CommunityThemeRegistry registry) : IThemeInstallationService
{
    /// <inheritdoc />
    public async Task<ThemeInstallation?> GetForOrgAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await EfThemeInstallationServiceHelpers.GetForOrgInternalAsync(
            db, orgId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ThemeInstallation> UpsertAsync(
        Guid orgId,
        string themeId,
        PlexorThemeManifest manifest,
        string signature,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        return await EfThemeInstallationServiceHelpers.UpsertInternalAsync(
            db, clock, registry, verifier, orgId, themeId, manifest, signature,
            actorUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await EfThemeInstallationServiceHelpers.DeleteInternalAsync(
            db, orgId, cancellationToken);
    }
}
