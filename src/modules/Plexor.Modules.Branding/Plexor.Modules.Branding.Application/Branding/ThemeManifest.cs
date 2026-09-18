// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeManifest — canonical wire shape for a marketplace community
// theme manifest. The host re-validates the publisher's signature
// against this canonical byte form before persisting the
// installation, so verifier and FE signer must agree on the
// canonicalisation order.
//
// Canonical form: `System.Text.Json` defaults of the manifest
// (declared-order PascalCase JSON via JsonSerializerOptions.Web),
// then UTF-8 bytes. Lives in Application so the
// IThemeInstallationService interface doesn't have to reach into
// Infrastructure.
// ============================================================================

namespace Plexor.Modules.Branding.Application.Branding;

/// <summary>
///     Canonical manifest describing a marketplace community theme.
///     The <see cref="ThemeId" /> is the stable id from the registry
///     (<c>"synthwave-night-dark"</c>, <c>"paper-light"</c>, ...);
///     <see cref="TokenValues" /> carries the oklch token vocabulary
///     keyed by the design-system token name
///     (<c>"background"</c>, <c>"foreground"</c>, <c>"accent"</c>, ...).
///     The signer adds <see cref="Author" /> + <see cref="Version" />
///     so a downstream verifier can detect a manifest reissue with
///     the same id but a different bundle.
/// </summary>
/// <param name="ThemeId">Stable marketplace id of the theme.</param>
/// <param name="Name">Human label shown in the marketplace UI.</param>
/// <param name="Version">Publisher-supplied SemVer string
/// (<c>"0.1.0"</c>).</param>
/// <param name="Author">Publisher handle or org name
/// (<c>"plexor-themes"</c>).</param>
/// <param name="TokenValues">The full token vocabulary + values
/// the boot script applies. Keyed by design-system token name.</param>
public sealed record ThemeManifest(
    string ThemeId,
    string Name,
    string Version,
    string Author,
    IReadOnlyDictionary<string, string> TokenValues);

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
