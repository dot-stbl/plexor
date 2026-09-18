// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CommunityThemeRegistry — the host-side mirror of the FE bundle
// in `web/apps/console/src/shared/lib/themes/community-themes.ts`.
// The marketplace endpoint looks the themeId up here before
// persisting, so the host doesn't have to trust the FE bundle to
// know what themes exist.
//
// Phase 5+ will swap this for a publisher feed whose manifests
// are signed with a different key (Ed25519 / RSA). The
// CommunityTheme interface stays identical so callers don't
// change.
// ============================================================================

namespace Plexor.Modules.Branding.Infrastructure.Branding;

/// <summary>
///     Host-side registry of marketplace community themes.
///     <see cref="TryFind(string)" /> returns the canonical record
///     for the supplied id, or <see langword="null" /> when no
///     theme matches.
/// </summary>
/// <param name="themes">Canonical theme records shipped with the
/// host bundle. Ordering doesn't matter — lookup is by id.</param>
public sealed class CommunityThemeRegistry(
    IReadOnlyDictionary<string, CommunityTheme> themes)
{
    /// <summary>
    ///     Look up a community theme by id. Returns
    ///     <see langword="null" /> when no theme matches — the
    ///     controller maps that to a 404 ProblemDetails.
    /// </summary>
    /// <param name="themeId">Stable marketplace id
    /// (<c>"synthwave-night-dark"</c>, <c>"paper-light"</c>, ...).</param>
    public CommunityTheme? TryFind(string themeId)
    {
        return themes.GetValueOrDefault(themeId);
    }

/// <summary>
///     Enumerate every registered theme. Used by the
///     <c>GET /api/v1/branding/theme/registry</c> diagnostic
///     endpoint in a future phase (and by integration tests
///     that need to assert the bundle list).
/// </summary>
public IReadOnlyCollection<CommunityTheme> All
    => themes.Values as IReadOnlyCollection<CommunityTheme>
        ?? themes.Values.ToArray();
}

/// <summary>
///     Minimal host-side record of a marketplace community theme —
///     id, name, version, author. The full token vocabulary lives
///     in the FE bundle; the host only needs the identifier + the
///     publisher metadata so it can re-validate a manifest
///     without trusting the FE bundle. Future publisher-feed
///     support adds the token vocabulary here.
/// </summary>
/// <param name="Id">Stable marketplace id.</param>
/// <param name="Name">Human label shown in the marketplace UI.</param>
/// <param name="Version">Publisher-supplied SemVer string.</param>
/// <param name="Author">Publisher handle or org name.</param>
public sealed record CommunityTheme(
    string Id,
    string Name,
    string Version,
    string Author);
