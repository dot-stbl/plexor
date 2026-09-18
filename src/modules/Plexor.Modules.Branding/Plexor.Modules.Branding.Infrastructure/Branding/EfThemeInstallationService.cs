// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfThemeInstallationService — EF-backed IThemeInstallationService.
// Wraps the scoped BrandingDbContext, the bundled
// CommunityThemeRegistry, the purpose-bound HMAC verifier, and
// the TimeProvider. The host is its own publisher in v1 (the
// theme marketplace ships with the host bundle, so the registry
// is the source of truth for what themes exist). Per-method
// helpers live in the sibling EfThemeInstallationServiceHelpers
// file so this class stays a thin orchestrator (no-private-
// methods convention, class-layout-and-tooling.md §1a).
// ============================================================================

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
///     EF change tracker after the verifier has signed the
///     canonical manifest.
/// </summary>
/// <param name="db">Scoped <see cref="Plexor.Modules.Branding.Infrastructure.Persistence.BrandingDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for
/// the <c>ActivatedAt</c> stamp.</param>
/// <param name="verifier">HMAC verifier that signs the canonical
/// manifest before persistence.</param>
/// <param name="registry">Hardcoded community-theme list that
/// maps a <c>themeId</c> to its canonical publisher metadata.</param>
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
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        return await EfThemeInstallationServiceHelpers.UpsertInternalAsync(
            db, clock, registry, verifier, orgId, themeId, actorUserId,
            cancellationToken);
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
