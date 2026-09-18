// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertThemeInstallationRequest — wire shape for
// PUT /api/v1/branding/theme. Tenant-scoped (the OrgId comes from
// the caller's bearer token; the body's ThemeId names which
// marketplace theme to activate).
//
// v1 simplification: the FE only sends the themeId. The host
// looks up the canonical manifest in CommunityThemeRegistry,
// signs it with the purpose-bound HMAC verifier, and persists
// both the manifest + signature back to the row. This avoids the
// key-distribution problem that would come from requiring the FE
// bundle to know the HMAC seed. A real publisher-feed world
// (Phase 5+ future) would extend this request with the signed
// manifest body and a publisher-feed key.
// ============================================================================

namespace Plexor.Modules.Branding.Api.Models.Requests;

/// <summary>
///     Wire shape for the PUT /api/v1/branding/theme body. The
///     <see cref="ThemeId" /> names the marketplace theme to
///     activate; the host looks it up in
///     <c>CommunityThemeRegistry</c> and signs the canonical
///     manifest before persisting. Unknown ids fail with 404.
/// </summary>
public sealed class UpsertThemeInstallationRequest
{
    /// <summary>Stable marketplace id from the host registry
    /// (<c>"synthwave-night-dark"</c>, <c>"paper-light"</c>, ...).
    /// The controller looks this up in
    /// <c>CommunityThemeRegistry</c>; unknown ids fail with 404.</summary>
    public string ThemeId { get; init; } = string.Empty;
}

