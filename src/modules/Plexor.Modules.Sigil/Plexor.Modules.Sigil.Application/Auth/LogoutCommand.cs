// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LogoutCommand + LogoutResult — revoke the presented refresh token.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Logout request — revoke the presented refresh token so it
///     can't be rotated again. Always idempotent (calling logout
///     twice returns the same result).
/// </summary>
/// <param name="RefreshToken">
///     The refresh token to revoke. May be empty / null — the
///     handler treats that as a no-op success so logout calls don't
///     fail when the client has already lost the token (e.g. after
///     a tab close).
/// </param>
public sealed record LogoutCommand(string? RefreshToken);

/// <summary>
///     Logout outcome. <see cref="RevokedTokens" /> is informational
///     only — it's always either 0 or 1 in v0.1 (single-token
///     revocation). Future per-device logout will return higher counts.
/// </summary>
/// <param name="RevokedTokens">
///     Number of refresh tokens marked revoked (0 when the token
///     was unknown / already revoked / not supplied).
/// </param>
public sealed record LogoutResult(int RevokedTokens);
