// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ChangePasswordCommand — change a user's password (current → new).
// Used both for forced first-login rotation AND voluntary password
// changes from the dashboard. Verifies current password before
// overwriting; invalidates active sessions by revoking every
// refresh-token family the user owns.
// ==========================================================================

namespace Plexor.Modules.Sigil.Application.Users;

/// <summary>
///     Change a user's password. The supplied <c>CurrentPassword</c>
///     must match the stored hash (PBKDF2-HMAC-SHA256) — the handler
///     returns the same generic error (<c>identity.credentials.invalid</c>)
///     whether the current password was wrong or the user is
///     unknown, to prevent account-enumeration leaks.
/// </summary>
/// <param name="UserId">Target user (must equal the authenticated
/// caller's id — admins use a separate path that bypasses the
/// current-password check).</param>
/// <param name="CurrentPassword">Plain-text current password (for
/// verification).</param>
/// <param name="NewPassword">Plain-text new password (minimum 8
/// characters, validated server-side).</param>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword);

/// <summary>
///     Result of <see cref="ChangePasswordCommand" />. The
/// <c>RefreshTokensRevoked</c> count is informational (always either
/// 0 or 1 in v0.1 — we revoke by family, and each post-change login
/// starts a fresh family).
/// </summary>
/// <param name="UserId">Echo of the target user.</param>
/// <param name="RefreshTokensRevoked">Number of refresh-token families
/// nuked as a side effect of the change.</param>
public sealed record ChangePasswordResult(
    Guid UserId,
    int RefreshTokensRevoked);
