// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HmacThemeManifestVerifier — HMAC-SHA256 over the canonical
// manifest bytes (declared-order JSON → UTF-8). Key material is
// derived from the host's IDataProtectionProvider via a
// purpose-scoped protector; the host registers
// AddDataProtection().PersistKeysToFileSystem(...) at startup so
// the key ring survives restarts.
//
// Why HMAC-SHA256 (not a public-key signature): the verifier and
// the publisher are the same trust zone — both ship with the host
// bundle — so symmetric HMAC suffices. A publisher-feed world
// (Phase 5+ future) would migrate to RSA / Ed25519 and swap this
// implementation.
//
// Determinism. IDataProtector.Protect() embeds a random nonce per
// call (so the same plaintext encrypts to a different ciphertext
// every time), which means a naive Use-Protect-Output-As-HMAC-Key
// pattern round-trips its own key differently on each call. The
// cipher is, however, deterministic in the inverse direction:
// Unprotect(Protect(x)) == x. The constructor caches one Protect
// ciphertext at startup, then Unprotect()s it on every Sign/Verify
// call so both operations see the same HMAC key. Singleton
// lifetime guarantees a single instance per process; both call
// sites (the in-process sign path during test + the in-process
// verify path during the controller's PUT handler) hit the same
// cached key.
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Plexor.Modules.Branding.Application.Branding;

namespace Plexor.Modules.Branding.Infrastructure.ThemeManifests;

/// <summary>
///     HMAC-SHA256 implementation of <see cref="IThemeManifestVerifier" />.
///     Key material is derived from the host's
///     <see cref="IDataProtectionProvider" /> through a
///     purpose-scoped protector so the same data-protection key ring
///     that protects cookies / API keys / OIDC state also pins the
///     theme-signing key.
/// </summary>
/// <remarks>
///     <para><b>Canonicalisation.</b> The manifest is serialised via
///     <see cref="JsonSerializerOptions.Web" /> (frozen, shared) into
///     a UTF-8 byte array; field order is the
///     <see cref="ThemeManifest" /> record's declaration order. The
///     signer and the verifier must agree on the canonical bytes —
///     changing either side silently invalidates every existing
///     installation row.</para>
///     <para><b>Key extraction.</b> The HMAC key is a round-tripped
///     ciphertext under a constant seed (see class remarks). Both
///     <see cref="SignManifest" /> and <see cref="VerifyManifest" />
///     hit the same singleton-scoped key buffer.</para>
/// </remarks>
public sealed class HmacThemeManifestVerifier : IThemeManifestVerifier
{
    /// <summary>Purpose string attached to every theme-manifest
    /// derivation. Keeping it distinct from any other purpose
    /// means the theme-signing key can rotate independently of the
    /// rest of the data-protection ring.</summary>
    private const string Purpose = "ThemeManifest.Sign";

    /// <summary>Stable seed fed through
    /// <see cref="IDataProtector.Protect(byte[])" /> once at startup;
    /// the resulting ciphertext is round-tripped through
    /// <see cref="IDataProtector.Unprotect(byte[])" /> on every
    /// Sign / Verify call to recover the seed as the HMAC key.</summary>
    private static readonly byte[] SeedSalt =
        Encoding.UTF8.GetBytes("Plexor.Host.ThemeManifest.Sign.v1");

    private static readonly JsonSerializerOptions CanonicalJson = JsonSerializerOptions.Web;

    private readonly IDataProtector protector;
    private readonly byte[] hmacKey;

    /// <summary>
    ///     Construct the verifier. The HMAC key is captured here
    ///     (one <see cref="IDataProtector.Protect(byte[])" /> +
    ///     <see cref="IDataProtector.Unprotect(byte[])" /> round
    ///     trip) so the constant seed becomes the deterministic
    ///     key used by both <see cref="SignManifest" /> and
    ///     <see cref="VerifyManifest" />. The round-trip is needed
    ///     because the cipher is non-deterministic in the forward
    ///     direction (random nonce) but deterministic in the
    ///     inverse direction (Unprotect always returns the same
    ///     plaintext for the same ciphertext). Singleton
    ///     registration keeps a single instance per process; both
    ///     call sites hit the same cached key.
    /// </summary>
    /// <param name="provider">Host's data-protection provider. The
    /// purpose-scoped protector <c>"ThemeManifest.Sign"</c> is
    /// minted under this provider.</param>
    public HmacThemeManifestVerifier(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector(Purpose);
        var ciphertext = protector.Protect(SeedSalt);
        hmacKey = protector.Unprotect(ciphertext);
    }

    /// <inheritdoc />
    public string SignManifest(ThemeManifest manifest)
    {
        var bytes = Canonicalise(manifest);
        var hash = HMACSHA256.HashData(hmacKey, bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc />
    public void VerifyManifest(ThemeManifest manifest, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new ThemeManifestVerificationException(
                "Manifest signature is required.");
        }

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            throw new ThemeManifestVerificationException(
                "Manifest signature is not valid hex.");
        }

        if (supplied.Length is not 32)
        {
            throw new ThemeManifestVerificationException(
                $"Manifest signature must be 32 bytes (got {supplied.Length}).");
        }

        var bytes = Canonicalise(manifest);
        var expected = HMACSHA256.HashData(hmacKey, bytes);

        // CryptographicOperations.FixedTimeEquals — constant-time
        // so the API doesn't leak a timing oracle that an attacker
        // could iterate byte-by-byte against.
        if (!CryptographicOperations.FixedTimeEquals(supplied, expected))
        {
            throw new ThemeManifestVerificationException(
                "Manifest signature does not match the canonical manifest bytes.");
        }
    }

    /// <summary>
    ///     Serialise <paramref name="manifest" /> to the canonical
    ///     byte form (declared-order JSON via
    ///     <see cref="JsonSerializerOptions.Web" /> → UTF-8). The
    ///     signer and the verifier must agree on this form; changing
    ///     it silently invalidates every previously-installed
    ///     manifest signature.
    /// </summary>
    private static byte[] Canonicalise(ThemeManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, CanonicalJson);
        return Encoding.UTF8.GetBytes(json);
    }
}
