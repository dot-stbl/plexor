// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TokenHasher — SHA-256 hashing helper for join-token secrets + a
// cryptographically random plaintext generator. Centralised here
// because:
// 1. The hash operation is IO-flagged by VSTHRD103 (SHA256.HashData
//    blocks synchronously; HashDataAsync needs a Stream).
// 2. Four call sites (create cluster, rotate token, node join,
//    node-bearer token) all want the same SHA-256-lowercase-hex + hex
//    random helpers — DRY.
// ============================================================================

using System.Security.Cryptography;
using System.Text;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     SHA-256 hashing helper for join-token secrets. Async to satisfy
///     VSTHRD103 (SHA256.HashData blocks; HashDataAsync needs a Stream).
/// </summary>
public static class TokenHasher
{
    /// <summary>
    ///     Hash the secret to a lowercase hex string.
    /// </summary>
    /// <param name="secret">The plaintext secret (already lowercased by the caller if needed).</param>
    /// <param name="cancellationToken">Forwarded to the hash stream.</param>
    /// <returns>64-char lowercase hex SHA-256 digest.</returns>
    public static async Task<string> HashAsync(string secret, CancellationToken cancellationToken)
    {
        var rawBytes = Encoding.UTF8.GetBytes(secret);
        await using var stream = new MemoryStream(rawBytes, writable: false);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken))
            .ToLowerInvariant();
    }

    /// <summary>
    ///     Generate a 256-bit cryptographically random secret, returned
    ///     as a 64-char lowercase hex string. Used as the plaintext half
    ///     of the (secret_hash, secret) pair — caller MUST hash with
    ///     <see cref="HashAsync" /> before persisting.
    /// </summary>
    public static string NewSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();
    }
}
