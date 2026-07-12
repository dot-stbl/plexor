// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IssuedTokens — internal carrier for the access+refresh pair returned
// by IAuthenticationService. Not a public DTO — controllers reshape
// it into LoginResult/RefreshResult on the way out.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     A signed access JWT plus the opaque refresh token the client
///     must send back to rotate. <see cref="AccessTokenExpiresAtUtc" />
///     is the <c>exp</c> claim copied verbatim so the controller can
///     set an HTTP cookie expiry or surface it for debugging.
/// </summary>
/// <param name="AccessToken">
///     Compact-serialized JWT (<c>header.payload.signature</c>). The
///     handler (Phase 3.6) reads this verbatim off the
///     <c>Authorization: Bearer</c> header.
/// </param>
/// <param name="RefreshToken">
///     Opaque base64url string (32 random bytes → 43 chars). Sent on
///     <c>POST /auth/refresh</c>. Stored in DB as its SHA-256 hash;
///     the raw bytes are never persisted.
/// </param>
/// <param name="AccessTokenExpiresAtUtc">
///     UTC instant of access-token expiry. Matches the JWT <c>exp</c>
///     claim to the second.
/// </param>
public sealed record IssuedTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc);
