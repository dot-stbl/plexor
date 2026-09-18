// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeManifest — canonical wire shape for a marketplace community
// theme manifest. The host signs and verifies the publisher's
// identity against this canonical byte form before persisting the
// installation. Lives in Application so the IThemeInstallationService
// interface doesn't have to reach into Infrastructure.
//
// v1 simplification: only the publisher metadata (id + name +
// version + author) is canonicalised; the token vocabulary stays
// in the FE bundle because Phase 5+ doesn't have a publisher feed
// to ship tokens server-side yet. A future commit that wires a real
// publisher feed would extend this record with the tokenValues
// field and have the FE ship a signed manifest body.
// ============================================================================

namespace Plexor.Modules.Branding.Application.Branding;

/// <summary>
///     Canonical publisher-metadata manifest for a marketplace
///     community theme. The host signs and verifies the publisher's
///     identity against this canonical byte form before persisting
///     the installation.
/// </summary>
/// <param name="ThemeId">Stable marketplace id of the theme
/// (<c>"synthwave-night-dark"</c>, <c>"paper-light"</c>, ...).</param>
/// <param name="Name">Human label shown in the marketplace UI
/// (<c>"Synthwave Night — Dark"</c>).</param>
/// <param name="Version">Publisher-supplied SemVer string
/// (<c>"0.1.0"</c>).</param>
/// <param name="Author">Publisher handle or org name
/// (<c>"plexor-themes"</c>).</param>
public sealed record ThemeManifest(
    string ThemeId,
    string Name,
    string Version,
    string Author);

/// <summary>
///     Raised by <c>IThemeManifestVerifier.VerifyManifest</c>
///     when the supplied signature does not match a fresh HMAC
///     over the canonical manifest bytes. The API boundary maps
///     this to a 400 ProblemDetails with code
///     <c>branding.manifest.invalid_signature</c>.
/// </summary>
/// <param name="message">Human-readable cause (no PII; the
/// manifest payload is the request body, not a credential).</param>
public sealed class ThemeManifestVerificationException(string message)
    : Exception(message)
{
}

/// <summary>
///     Raised by <c>IThemeInstallationService.UpsertAsync</c>
///     when the supplied <c>themeId</c> doesn't match any entry
///     in the host-side community-theme registry. Maps to a 404
///     at the API boundary.
/// </summary>
/// <param name="themeId">The unknown id, as supplied by the
/// caller.</param>
public sealed class UnknownThemeException(string themeId)
    : Exception($"Unknown theme id: '{themeId}'.")
{
    /// <summary>The unknown id that triggered the exception.</summary>
    public string ThemeId { get; } = themeId;
}
