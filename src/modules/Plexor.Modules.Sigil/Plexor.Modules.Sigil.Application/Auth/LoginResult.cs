// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoginResult — public DTO returned by the auth endpoints. Different
// from IssuedTokens (internal carrier) so callers don't accidentally
// couple to signing-internal types.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Result of a successful login / refresh round-trip.
/// </summary>
/// <param name="AccessToken">
///     Compact-serialized JWT (<c>header.payload.signature</c>).
///     Set the <c>Authorization: Bearer</c> header on subsequent
///     requests.
/// </param>
/// <param name="RefreshToken">
///     Opaque base64url string. Send verbatim on
///     <c>POST /auth/refresh</c> to rotate.
/// </param>
/// <param name="AccessTokenExpiresAtUtc">
///     UTC instant of access-token expiry. Mirrors the JWT
///     <c>exp</c> claim to the second; clients may pre-empt refresh
///     by reading this.
/// </param>
public sealed record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc);
