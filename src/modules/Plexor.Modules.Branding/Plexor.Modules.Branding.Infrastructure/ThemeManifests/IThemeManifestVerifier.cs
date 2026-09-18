// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IThemeManifestVerifier — sign / verify the HMAC-SHA256 of a
// community-theme manifest. The marketplace persists the
// signature on every install; the boot script on the FE
// re-verifies the same signature against window.__PLEXOR_CONFIG__
// before applying the tokens so a tampered bundle can't apply
// unauthorised themes to the running console.
//
// Key material is derived from the host's IDataProtectionProvider
// (purpose-scoped `ThemeManifest.Sign`) rather than re-invented;
// production deployments rotate keys via the standard data-protection
// key ring persisted under <DATAPROTECTION_KEYS_DIR>.
// ============================================================================

using Plexor.Modules.Branding.Application.Branding;

namespace Plexor.Modules.Branding.Infrastructure.ThemeManifests;

/// <summary>
///     Sign / verify a community-theme manifest via HMAC-SHA256
///     over the canonical manifest bytes. Key material is derived
///     from the host's <c>IDataProtectionProvider</c>; production
///     deployments rotate keys via the standard data-protection key
///     ring.
/// </summary>
public interface IThemeManifestVerifier
{
    /// <summary>
    ///     Sign a manifest. Returns the hex-encoded HMAC-SHA256
    ///     of the canonical manifest bytes (declared-order JSON →
    ///     UTF-8). The caller stores the hex string in
    ///     <c>branding.theme_installations.manifest_signature</c>.
    /// </summary>
    /// <param name="manifest">The manifest bytes to sign.</param>
    /// <returns>Lowercase hex (64 chars, 32 bytes).</returns>
    public string SignManifest(ThemeManifest manifest);

    /// <summary>
    ///     Verify a signature against the manifest. Throws
    ///     <see cref="ThemeManifestVerificationException" /> on a
    ///     mismatch (tampered manifest, wrong key, wrong encoding).
    ///     Verification is constant-time so a timing oracle doesn't
    ///     leak which byte failed.
    /// </summary>
    /// <param name="manifest">The manifest to verify.</param>
    /// <param name="signature">Lowercase hex signature from the
    /// request body or stored row.</param>
    public void VerifyManifest(ThemeManifest manifest, string signature);
}
