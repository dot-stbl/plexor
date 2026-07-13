// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IJwtSigningService — Application-layer contract for signing access
// tokens. Infrastructure binds an ECDSA-backed implementation that
// uses System.IdentityModel.Tokens.Jwt.
// ============================================================================

using System.Security.Claims;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Issues + verifies compact JWTs (ES256 / ECDSA P-256) for the
///     Plexor auth flow. Returns <see cref="IssuedTokens" /> on
///     issue; <see cref="VerifyResult" /> on verify.
/// </summary>
/// <remarks>
///     <para><b>Algorithm.</b> ES256 (ECDSA P-256 / SHA-256). 64-byte
///     signatures, 32-byte public keys — half the size of RSA-2048.
///     Same security level (NIST ~128-bit) as RSA-3072.</para>
///     <para><b>Key rotation.</b> Every JWT carries a <c>kid</c>
///     header. Verification looks up the public key by kid; on
///     miss, falls back to scanning the active set (covers the
///     rotation window where the verifier hasn't cached the new
///     key yet).</para>
///     <para><b>Lifetime.</b> 15 minutes (sliding — refresh
///     extends). Lifetime lives here as a constant; the auth
///     controller copies it into the response.</para>
/// </remarks>
public interface IJwtSigningService
{
    /// <summary>Access-token lifetime. Refresh tokens live in
    /// <see cref="IRefreshTokenStore" /> with their own 30-day expiry.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Issue a compact JWT for a user. The
    ///     <c>sub</c>/<c>tid</c>/<c>role</c>/<c>permission</c>/<c>service</c>
    ///     claims are read from the supplied
    ///     <see cref="ClaimsPrincipal" /> via <see cref="Plexor.Modules.Sigil.Application.Abstractions.IdentityClaims" />
    ///     constants.
    /// </summary>
    /// <param name="principal">Caller identity. Read once; the principal
    ///     is not mutated.</param>
    /// <param name="cancellationToken">Forwarded to key-lookup IO.</param>
    /// <returns>
    ///     Issued JWT (compact form) + its expiry instant (UTC).
    ///     Caller pairs this with a refresh token to form
    ///     <see cref="IssuedTokens" />.
    /// </returns>
    public Task<IssuedAccessToken> IssueAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verify a compact JWT and return the original
    ///     <see cref="ClaimsPrincipal" /> if valid. Used by the
    ///     bearer handler (Phase 3.6).
    /// </summary>
    /// <param name="compactJwt">The <c>header.payload.signature</c> string.</param>
    /// <param name="cancellationToken">Forwarded to key-lookup IO.</param>
    /// <returns>
    ///     <see cref="VerifyResult" /> — <see cref="VerifyResult.Success" />
    ///     carries the <see cref="ClaimsPrincipal" />; failures
    ///     carry a reason for diagnostics + 401 mapping.
    /// </returns>
    public Task<VerifyResult> VerifyAsync(
        string compactJwt,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Compact JWT + its <c>exp</c> instant. The auth controller
///     pairs this with a refresh token to build
///     <see cref="IssuedTokens" />.
/// </summary>
/// <param name="CompactJwt">Compact serialized JWT — header.payload.signature.</param>
/// <param name="ExpiresAtUtc">UTC instant matching the JWT <c>exp</c> claim.</param>
public sealed record IssuedAccessToken(string CompactJwt, DateTimeOffset ExpiresAtUtc);

/// <summary>
///     Discriminated outcome of <see cref="IJwtSigningService.VerifyAsync" />.
///     Uses a sealed record hierarchy instead of an enum + out-param so
///     the caller pattern-matches on the result.
/// </summary>
public abstract record VerifyResult
{
    private VerifyResult() { }

    /// <summary>Valid JWT, with its claims. The principal's identity
    /// is set; downstream code reads <see cref="ClaimsPrincipal.FindFirst(string)" />.</summary>
    /// <param name="Principal"></param>
    public sealed record Success(ClaimsPrincipal Principal) : VerifyResult;

    /// <summary>JWT well-formed but signature failed or expired.</summary>
    /// <param name="Reason"></param>
    public sealed record Invalid(string Reason) : VerifyResult;

    /// <summary>JWT format itself is broken (malformed base64, missing dots).</summary>
    /// <param name="Reason"></param>
    public sealed record Malformed(string Reason) : VerifyResult;
}
