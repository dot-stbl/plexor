// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshTokenHasher — SHA-256 of a raw refresh token, base64url
// encoded. The DB stores only the hash; the raw bytes are returned
// to the client once and never persisted.
// ============================================================================

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     One-way hash of a raw refresh token for at-rest storage. The DB
///     column holds the hash; only the raw token is shown to the
///     client on issue / rotate.
/// </summary>
/// <remarks>
///     <para><b>Why SHA-256, not bcrypt / PBKDF2.</b> A refresh token
///     is already a 256-bit cryptographically random secret — adding
///     PBKDF2 on top would slow hashing without raising the security
///     bar. SHA-256 is the standard fast hash for "store this secret
///     so we can recognize it later." Tokens that are stolen from
///     the DB still require breaking the at-rest DB encryption
///     separately; SHA-256 gives us equality lookup without leaking
///     the token to disk dumps.</para>
///     <para><b>Why base64url-encoded hash.</b> Mirrors the encoding
///     used by <see cref="TokenGenerator" /> so the column is
///     43-char VARCHAR. (Binary storage in <c>bytea</c> would also
///     work but base64url keeps the schema identical to the raw
///     token format and plays nicely with copy-paste debugging.)</para>
///     <para><b>Why a salt.</b> We don't add one. The input is
///     already high-entropy (32 bytes from CSPRNG); a salt would
///     only protect against pre-computed rainbow tables, which are
///     irrelevant when each input is unique.</para>
/// </remarks>
public static class RefreshTokenHasher
{
    /// <summary>SHA-256 → base64url, no padding. 32 input bytes yield 43 chars.</summary>
    /// <param name="rawToken">The raw token string returned to the client.</param>
    /// <returns>43-char base64url-encoded SHA-256 digest.</returns>
    public static string Hash(string rawToken)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken), hash);
        return Base64Url.EncodeToString(hash);
    }
}
