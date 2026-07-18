// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IUserRevocationChecker — centralises the post-verify checks that turn
// "the JWT signature is valid" into "the caller is still authorised".
// Backs the JWT signing service so a stolen, signature-valid token
// becomes invalid the moment the user is disabled or rotates their
// password.
// ==========================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Returns whether an already-signature-validated bearer token is
/// still authorised. The JWT signing service calls this after
/// signature + lifetime checks succeed; a <c>Forbidden</c> result
/// downgrades the response from 200 to 401.
/// </summary>
/// <remarks>
///     <para><b>Why a separate service and not inline in JwtSigningService.</b>
///     Keeps the JWT signing service focused on the
///     signature + key-rotation question. Revocation is a separate
///     DB-backed concern — the same service is also reachable from
///     the refresh-token rotation handler in Phase 4+ when we add
///     per-session forced-logout.</para>
///     <para><b>Two reasons to revoke.</b>
///     <list type="number">
///       <item><b>User disabled.</b> <c>sigil.users.status = "suspended"</c>
///       stops every still-circulating bearer for that user.</item>
///       <item><b>Password rotation.</b>
///       <c>sigil.users.password_changed_at &gt; token_iat</c> rejects
///       every bearer issued before the rotation — even within the
///       15-minute access-token window. Without this check, a stolen
///       token stays valid for up to 15 minutes after the user has
///       rotated their password.</item>
///     </list></para>
/// </remarks>
public interface IUserRevocationChecker
{
    /// <summary>
    ///     Returns <c>true</c> when the user is still active AND has
    ///     not rotated their password since the token was issued.
    /// </summary>
    /// <param name="userId">User identity parsed from the JWT
    /// <c>sub</c> claim.</param>
    /// <param name="tokenIssuedAtUtc">Token <c>iat</c> claim. Used to
    /// decide whether a password rotation that happened after the
    /// token was issued should invalidate it.</param>
    /// <param name="cancellationToken">Forwarded to the DB lookup.</param>
    public Task<RevocationCheckResult> IsStillValidAsync(
        Guid userId,
        DateTimeOffset tokenIssuedAtUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Three-state revocation check outcome. <see cref="Active" /> is
/// the only "allow" path; the other two surface as 401 with
/// distinguishable reasons (used by the bearer handler to write
/// the matching <c>WWW-Authenticate: error="invalid_token",
/// error_description="..."</c> header per RFC 6750 §3).
/// </summary>
public abstract record RevocationCheckResult
{
    private RevocationCheckResult() { }

    /// <summary>User is active and no password rotation has happened
    /// since the token was issued. Bearer may proceed.</summary>
    public sealed record Active : RevocationCheckResult;

    /// <summary>User does not exist (deleted) or
    /// <c>status = "suspended"</c>. Bearer must be rejected.</summary>
    /// <param name="Reason"></param>
    public sealed record UserDisabled(string Reason) : RevocationCheckResult;

    /// <summary>User has rotated their password after the token was
    /// issued; the bearer predates the rotation and must be
    /// rejected. RFC 6750 §3 <c>error="invalid_token"</c>.</summary>
    /// <param name="rotatedAt"></param>
    public sealed record PasswordRotated(DateTimeOffset rotatedAt) : RevocationCheckResult;
}
