// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IOrgAuthProviderSecretProtector — read-side seam for the OIDC client
// secret encryption wrapper. Lets the OIDC token client
// (Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc.OidcTokenClient)
// decrypt the per-tenant client secret without depending on the
// concrete OrgAuthProviderSecretProtector in
// Plexor.Modules.Realm.Infrastructure.AuthProviders — Law 3 of the
// modular-monolith architecture forbids cross-module Infrastructure
// references.
//
// Encrypt is exposed here too so a future write-side caller in
// Sigil (e.g. an OIDC admin surface) doesn't reintroduce a direct
// dependency on the concrete wrapper. Today's only consumer is the
// host controller (composition root, allowed by Law 4).
//
// Lifetime: Singleton (mirrors the concrete wrapper).
// ============================================================================

namespace Plexor.Modules.Realm.Application.AuthProviders;

/// <summary>
///     Purpose-bound encryption / decryption seam for the OIDC
///     client secret stored in
///     <c>realm.org_auth_provider_configs.oidc_client_secret_protected</c>.
///     Lets callers outside the Realm module encrypt and decrypt the
///     per-tenant secret without taking a dependency on
///     <c>Plexor.Modules.Realm.Infrastructure.AuthProviders.OrgAuthProviderSecretProtector</c>.
/// </summary>
public interface IOrgAuthProviderSecretProtector
{
    /// <summary>
    ///     Decrypt a stored ciphertext back to plaintext. Returns
    ///     <c>null</c> when <paramref name="protectedBase64" /> is
    ///     null or empty — the OIDC token client treats that as
    ///     "no secret configured for this tenant" and short-circuits.
    ///     Keyring-rotation or malformed-ciphertext exceptions
    ///     propagate to the caller, which logs and falls back to a
    ///     structured "decrypt failed" outcome.
    /// </summary>
    /// <param name="protectedBase64">The base64-encoded protected
    /// payload stored in
    /// <c>realm.org_auth_provider_configs.oidc_client_secret_protected</c>.</param>
    /// <returns>The plaintext secret, or <c>null</c> when no
    /// ciphertext was stored.</returns>
    public string? Decrypt(string? protectedBase64);

    /// <summary>
    ///     Encrypt a plaintext secret for storage. Returns a
    ///     base64-encoded protected payload suitable for the
    ///     <c>oidc_client_secret_protected</c> column. Today's
    ///     caller is the host controller; future Sigil-admin
    ///     surfaces that need to write a secret go through this
    ///     interface too.
    /// </summary>
    /// <param name="plaintext">Plaintext client secret supplied
    /// by the org admin.</param>
    /// <returns>Base64-encoded protected payload.</returns>
    public string Encrypt(string plaintext);
}
