// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IPkceGenerator — PKCE (RFC 7636) generator seam. Phase 4.6.3a —
// consumed by the OIDC flow endpoints (4.6.3b) to mint a fresh
// code_verifier + code_challenge pair at the start of the authorize
// leg, and verified by the token client (4.6.3a) on the callback leg.
//
// PKCE mechanics:
//   - code_verifier — 43–128 char URL-safe base64-no-padding random
//     string (43 chars = 32 random bytes).
//   - code_challenge — base64url(SHA-256(code_verifier)) when method =
//     "S256" (the only method this generator emits).
//   - method — "S256" (RFC 7636 §4.2); the "plain" method is
//     deprecated by RFC 7636 §7.2 and forbidden by OpenID Connect
//     Core 1.0 §15.5.2 for confidential clients.
// ============================================================================

namespace Plexor.Shared.Kernel.AuthProviders;

/// <summary>
///     Generate a fresh PKCE pair for the OIDC authorization-code
///     flow (RFC 7636). The generator is stateless; the OIDC flow
///     caller owns the lifetime (the verifier lives until the
///     callback exchanges the code, then is dropped).
/// </summary>
/// <remarks>
///     <para><b>Lifetime.</b> <see cref="IPkceGenerator" /> is
///     stateless; <c>Singleton</c> lifetime is the right shape.
///     The <c>RandomNumberGenerator</c> it depends on is
///     thread-safe.</para>
/// </remarks>
public interface IPkceGenerator
{
    /// <summary>
    ///     Generate a fresh PKCE pair. <see cref="PkcePair.CodeVerifier" />
    ///     is a 43-char URL-safe base64 random string (32 bytes);
    ///     <see cref="PkcePair.CodeChallenge" /> is the SHA-256 hash
    ///     of the verifier, base64url-encoded (RFC 7636 §4.2 S256
    ///     method).
    /// </summary>
    /// <returns>The pair. Both halves are URL-safe base64 with no
    /// padding (RFC 7636 §4.1).</returns>
    public PkcePair Generate();
}

/// <summary>
///     One PKCE pair (RFC 7636). Three pieces:
///     <see cref="CodeVerifier" /> is sent on the token-exchange
///     leg (4.6.3a) to prove the caller that started the authorize
///     leg is the same caller that's finishing it;
///     <see cref="CodeChallenge" /> + <see cref="Method" /> are sent
///     on the authorize leg so the IDP can verify the verifier
///     later.
/// </summary>
/// <param name="CodeVerifier">43-char URL-safe base64 random
/// string. Stored server-side, sent in the token-exchange POST.</param>
/// <param name="CodeChallenge">Base64url(SHA-256(CodeVerifier)).
/// Sent in the authorize redirect.</param>
/// <param name="Method">Always <c>"S256"</c> — RFC 7636 §4.2;
/// the "plain" method is forbidden for confidential clients
/// (OpenID Connect Core 1.0 §15.5.2).</param>
public sealed record PkcePair(
    string CodeVerifier,
    string CodeChallenge,
    string Method);
