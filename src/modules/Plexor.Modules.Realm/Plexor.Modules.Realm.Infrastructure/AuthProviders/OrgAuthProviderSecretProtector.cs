// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderSecretProtector — purpose-bound IDataProtector wrapper
// for the OIDC client secret (Phase 4.6.3a — moved from Plexor.Host).
//
// The host's controller (Phase 4.6.1) used to mint a purpose-bound
// IDataProtector inline at the call site via
//   dataProtectionProvider.CreateProtector(OrgAuthProviderSecretProtector.Purpose)
// This class centralises that purpose string + the
// protect/unprotect helpers so the OIDC token client (4.6.3a) can
// reuse the same protector without re-declaring the purpose.
//
// Why a typed wrapper rather than a static helper:
//   1. The purpose string lives with the wrapper — moving the
//      controller to a different purpose (e.g. "OIDC client secret v2")
//      only changes one file.
//   2. Unit tests can construct the wrapper directly against a fake
//      IDataProtectionProvider without going through the host's
//      keyring configuration.
//   3. The Decrypt method intentionally tolerates an empty/null
//      ciphertext — the controller's "test" endpoint + the OIDC
//      token client both handle "secret not configured" by short-
//      circuiting on null before doing anything else.
//
// Lifetime: Singleton. The IDataProtector returned by
// IDataProtectionProvider.CreateProtector is thread-safe; the wrapper
// holds no per-request state.
// ============================================================================

using Microsoft.AspNetCore.DataProtection;
using Plexor.Modules.Realm.Application.AuthProviders;

namespace Plexor.Modules.Realm.Infrastructure.AuthProviders;

/// <summary>
///     Purpose-bound <see cref="IDataProtector" /> wrapper for the
///     <c>realm.org_auth_provider_configs.oidc_client_secret_protected</c>
///     column. Encapsulates the purpose string so callers cannot mint
///     a different-purpose protector by accident.
/// </summary>
/// <remarks>
///     Build the wrapper against the host's
///     <see cref="IDataProtectionProvider" />. The provider is the
///     one registered in <c>Plexor.Host/Program.cs</c> via
///     <c>AddDataProtection()</c>.
/// </remarks>
/// <param name="provider">The host's
/// <see cref="IDataProtectionProvider" />.</param>
public sealed class OrgAuthProviderSecretProtector(IDataProtectionProvider provider)
    : IOrgAuthProviderSecretProtector
{
    /// <summary>
    ///     Stable purpose discriminator. Different-purpose protectors
    ///     minted elsewhere cannot decrypt the same ciphertext — the
    ///     data-protection keyring keys the ciphertext by purpose, so
    ///     a "default" protector minted at the application root would
    ///     fail to unprotect anything written by this protector and
    ///     vice versa.
    /// </summary>
    public const string Purpose = "OrgAuthProviderConfig.OidcClientSecret";

    private readonly IDataProtector protector = provider.CreateProtector(Purpose);

    /// <summary>
    ///     Decrypt a stored ciphertext back to plaintext. Returns
    ///     <c>null</c> when <paramref name="protectedBase64" /> is
    ///     null or empty — the OIDC token client treats that as "no
    ///     secret configured for this tenant" and short-circuits.
    ///     Other exceptions (keyring rotation, malformed ciphertext)
    ///     propagate to the caller, which logs and falls back to a
    ///     structured "decrypt failed" outcome.
    /// </summary>
    /// <param name="protectedBase64">The base64-encoded protected
    /// payload stored in
    /// <c>realm.org_auth_provider_configs.oidc_client_secret_protected</c>.</param>
    /// <returns>The plaintext secret, or <c>null</c> when no
    /// ciphertext was stored.</returns>
    public string? Decrypt(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
        {
            return protectedBase64;
        }

        return protector.Unprotect(protectedBase64);
    }

    /// <summary>
    ///     Encrypt a plaintext secret for storage. Used by the
    ///     controller's PUT path; the OIDC token client never writes
    ///     — it only reads. Returns a base64-encoded protected
    ///     payload suitable for the
    ///     <c>oidc_client_secret_protected</c> column.
    /// </summary>
    /// <param name="plaintext">The plaintext client secret supplied
    /// by the org admin via PUT.</param>
    /// <returns>The base64-encoded protected payload.</returns>
    public string Encrypt(string plaintext)
    {
        return protector.Protect(plaintext);
    }
}
