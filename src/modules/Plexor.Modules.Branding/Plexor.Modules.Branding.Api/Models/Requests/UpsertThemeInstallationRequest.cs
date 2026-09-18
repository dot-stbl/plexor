// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertThemeInstallationRequest — wire shape for
// PUT /api/v1/branding/theme. Tenant-scoped (the path is the
// authoritative orgId; the body's Manifest.ThemeId is what we
// install).
// ============================================================================

namespace Plexor.Modules.Branding.Api.Models.Requests;

/// <summary>
///     Wire shape for the PUT /api/v1/branding/theme body. The
///     <see cref="ThemeId" /> names the marketplace theme to
///     activate; the <see cref="Manifest" /> carries the
///     publisher's token vocabulary + author + version; the
///     <see cref="Signature" /> is the hex-encoded HMAC-SHA256
///     over the canonical manifest bytes that the host re-checks
///     via <c>IThemeManifestVerifier</c> before persisting.
/// </summary>
/// <remarks>
///     Token values are intentionally NOT carried on the install
///     path — the marketplace publisher signs the manifest
///     (which already contains the tokens) and the host only has
///     to know that the manifest is genuine. Re-applying the
///     tokens is the FE boot script's job (it re-reads the
///     manifest from <c>window.__PLEXOR_CONFIG__</c> and applies
///     the tokens on every reload). The signature is the audit
///     trail.
/// </remarks>
public sealed class UpsertThemeInstallationRequest
{
    /// <summary>Stable marketplace id from the host registry
    /// (<c>"synthwave-night-dark"</c>, <c>"paper-light"</c>, ...).
    /// The controller looks this up in
    /// <c>CommunityThemeRegistry</c>; unknown ids fail with 404.</summary>
    public string ThemeId { get; init; } = string.Empty;

    /// <summary>Publisher-supplied canonical manifest: id, name,
    /// version, author, and the full token vocabulary the FE
    /// boot script applies.</summary>
    public ThemeManifestRequest Manifest { get; init; } = new();

    /// <summary>Hex-encoded HMAC-SHA256 over the canonical
    /// manifest bytes (declared-order JSON via
    /// <see cref="System.Text.Json.JsonSerializerOptions.Web" />
    /// → UTF-8). Verified by
    /// <c>IThemeManifestVerifier.VerifyManifest</c>.</summary>
    public string Signature { get; init; } = string.Empty;
}

/// <summary>
///     Wire shape for the manifest payload of
///     <see cref="UpsertThemeInstallationRequest" />. Mirrors the
///     host-side <c>ThemeManifest</c> record so the canonical
///     signature matching works without translation.
/// </summary>
public sealed class ThemeManifestRequest
{
    /// <summary>Stable marketplace id (must match the request's
    /// <see cref="UpsertThemeInstallationRequest.ThemeId" />).
    /// A mismatch is caught by the validator.</summary>
    public string ThemeId { get; init; } = string.Empty;

    /// <summary>Human label shown in the marketplace UI.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Publisher-supplied SemVer string.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Publisher handle or org name.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>The full token vocabulary the FE boot script
    /// applies. Keyed by design-system token name.</summary>
    public IDictionary<string, string> TokenValues { get; init; }
        = new Dictionary<string, string>();
}
