// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PkceGenerator — IPkceGenerator implementation (RFC 7636). Phase
// 4.6.3a. Lives in Plexor.Modules.Sigil.Infrastructure because the
// OIDC flow endpoints (4.6.3b) live in the Sigil module too; the
// shared interface lives in Plexor.Shared.Kernel so the test
// project can mock without dragging in Sigil.Infrastructure.
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using Plexor.Shared.Kernel.AuthProviders;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders;

/// <summary>
///     Stateless <see cref="IPkceGenerator" /> implementation.
///     Generates a 32-byte CSPRNG <c>code_verifier</c> (43 base64url
///     chars) + the SHA-256-derived <c>code_challenge</c>. Method is
///     always <c>"S256"</c> (RFC 7636 §4.2); the <c>"plain"</c>
///     method is forbidden by OpenID Connect Core 1.0 §15.5.2 for
///     confidential clients.
/// </summary>
/// <remarks>
///     <para><b>Thread safety.</b> The generator holds no state;
///     <see cref="RandomNumberGenerator" /> is the OS CSPRNG and is
///     thread-safe. <c>Singleton</c> lifetime is the right shape.</para>
///     <para><b>Why <see cref="System.Buffers.Text.Base64Url.EncodeToString" />.</b>
///     The 32-byte payload encodes to exactly 43 URL-safe base64
///     chars with no padding — the RFC 7636 §4.1 minimum length.
///     The base64url encoder (RFC 4648 §5) drops the
///     <c>+</c>/<c>/</c>/<c>=</c> characters that would need
///     escaping in URLs and form-encoded bodies.</para>
/// </remarks>
public sealed class PkceGenerator : IPkceGenerator
{
    /// <summary>32 random bytes → 43-char base64url string.</summary>
    private const int VerifierByteLength = 32;

    /// <inheritdoc />
    public PkcePair Generate()
    {
        Span<byte> verifierBytes = stackalloc byte[VerifierByteLength];
        RandomNumberGenerator.Fill(verifierBytes);

        // RFC 7636 §4.1 — the verifier is the ASCII encoding of the
        // random bytes. base64url-encoding is itself ASCII-safe, so
        // we hash the base64url string rather than the raw bytes
        // (RFC 7636 §4.2).
        var verifier = System.Buffers.Text.Base64Url.EncodeToString(verifierBytes);

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = System.Buffers.Text.Base64Url.EncodeToString(challengeBytes);

        return new PkcePair(verifier, challenge, "S256");
    }
}
