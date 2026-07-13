// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TokenGenerator — cryptographically-strong random tokens, base64url
// encoded. Used for refresh tokens (256 bits) and similar one-shot
// secrets. NOT for passwords — use IPasswordHasher.
// ============================================================================

using System.Security.Cryptography;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Cryptographically-strong random tokens. Uses
///     <see cref="RandomNumberGenerator.Fill(Span{byte})" /> for
///     unpredictable entropy and base64url-encodes the result so
///     tokens survive URL / header / cookie transport without
///     escaping.
/// </summary>
/// <remarks>
///     <para><b>Why RandomNumberGenerator, not Random.</b>
///     <see cref="System.Random" /> is a deterministic PRNG seeded
///     from a clock value — predictable from one output. Real secrets
///     must come from the OS CSPRNG (CNG / /dev/urandom / getrandom).</para>
///     <para><b>Why base64url, not base64 or hex.</b> base64url
///     (RFC 4648 §5) drops the <c>+</c>/<c>/</c>/<c>=</c> characters
///     that need escaping in URLs, headers, and JSON. The 32-byte
///     payload encodes to 43 chars — shorter than hex (64 chars)
///     and round-trippable.</para>
///     <para><b>Refresh token length.</b> 32 bytes = 256 bits of
///     entropy. Brute-forcing a single token requires 2^256 guesses —
///     far past computationally feasible. Even if a token leaks, it's
///     good for one rotation; the next refresh detects replay.</para>
/// </remarks>
public static class TokenGenerator
{
    /// <summary>Default byte length for refresh tokens — 256 bits.</summary>
    public const int DefaultRefreshTokenByteLength = 32;

    /// <summary>
    ///     Generate a cryptographically random token, base64url-encoded
    ///     with no padding. 32 input bytes yield 43 output chars.
    /// </summary>
    /// <param name="byteLength">
    ///     Bytes of randomness. Default
    ///     <see cref="DefaultRefreshTokenByteLength" /> = 32 (256 bits).
    ///     Must be in <c>[1, 256]</c>.
    /// </param>
    /// <returns>
    ///     base64url-encoded token. URL-safe, no padding
    ///     (<c>System.Buffers.Text.Base64Url</c> handles this).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="byteLength" /> is outside <c>[1, 256]</c>.
    /// </exception>
    public static string Generate(int byteLength = DefaultRefreshTokenByteLength)
    {
        if (byteLength is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength), byteLength, "Must be in [1, 256].");
        }

        Span<byte> buffer = stackalloc byte[byteLength];
        RandomNumberGenerator.Fill(buffer);
        return System.Buffers.Text.Base64Url.EncodeToString(buffer);
    }
}
