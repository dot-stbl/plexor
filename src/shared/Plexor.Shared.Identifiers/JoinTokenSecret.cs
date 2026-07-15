// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JoinTokenSecret — the raw credential a NodeAgent proves itself
// with during the join handshake.
//
// This type is intentionally NOT prefixed — it is a secret, not an
// ID. The column that stores this in hashed form is
// forge.join_tokens.secret_hash (PBKDF2-hashed via TokenHasher).
//
// Generation:    <see cref="New" /> returns 32 cryptographically
//                random bytes encoded as URL-safe base64 (no padding).
// Comparison:    never compared as string equality; the host always
//                hashes the candidate and compares in constant time
//                against the stored hash.
// Rotation:      every cluster-issuance produces a new secret; the
//                previous one is marked "revoked" and stops working.
// ============================================================================

using System.Security.Cryptography;

namespace Plexor.Shared.Identifiers;

/// <summary>
///     Raw credential for NodeAgent join. 32 bytes of CSPRNG output,
///     URL-safe base64 encoded (44 chars including two trailing `=`).
///     Not prefixed — this is a secret, not an identifier.
/// </summary>
/// <param name="Raw">
///     The decoded secret bytes. Persisted only as a PBKDF2 hash
///     via <c>TokenHasher</c>.
/// </param>
public readonly partial record struct JoinTokenSecret(byte[] Raw)
{
    /// <summary>Length in bytes of a freshly-generated secret. 32 = 256 bits.</summary>
    public const int ByteLength = 32;

    /// <summary>
    ///     Generate a new cryptographically random join secret.
    ///     Salt of an empty HashPassword call later in TokenHasher
    ///     adds another 16 bytes; the secret is the entropy source.
    /// </summary>
    public static JoinTokenSecret New()
    {
        Span<byte> buffer = stackalloc byte[ByteLength];
        RandomNumberGenerator.Fill(buffer);
        return new JoinTokenSecret(buffer.ToArray());
    }

    /// <summary>
    ///     String form, only ever used for one-time display to the
    ///     operator via <c>POST /clusters/{id}/tokens</c>. Never log
    ///     this property's value.
    /// </summary>
    public override string ToString()
    {
        var b64 = Convert.ToBase64String(Raw);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
