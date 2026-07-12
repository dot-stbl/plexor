// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IRefreshTokenStore — refresh-token CRUD with rotation + replay
// detection. Application-layer contract; Infrastructure binds an
// EF Core implementation.
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Persistence boundary for <see cref="RefreshToken" /> records.
///     Auth issuance/rotation lives in <c>IAuthenticationService</c>
///     (3.2.b); this contract only covers the storage operations so
///     the auth service can be tested against an in-memory fake.
/// </summary>
/// <remarks>
///     <para><b>Rotation chain.</b> Each refresh rotates: the old
///     token is marked <see cref="RefreshToken.RevokedAt" /> with
///     <see cref="RefreshToken.ReplacedBy" /> pointing at the new
///     token's id. The chain is rooted by
///     <see cref="RefreshToken.FamilyId" /> — shared across every
///     rotation of the same login session.</para>
///     <para><b>Replay detection.</b> If a token whose
///     <see cref="RefreshToken.RevokedAt" /> is non-null is presented
///     again, <see cref="RotateAsync" /> returns
///     <see cref="RefreshRotationResult.Replayed" />. The auth
///     service MUST then call
///     <see cref="RevokeFamilyAsync" /> to nuke every token in
///     the family — the chain is compromised.</para>
///     <para><b>Storage shape.</b> Only
///     <see cref="RefreshToken.TokenHash" /> (SHA-256 base64url) is
///     persisted; the raw token is shown to the client once on
///     issue and never stored.</para>
/// </remarks>
public interface IRefreshTokenStore
{
    /// <summary>
    ///     Issue a fresh refresh token (login flow). The token
    ///     belongs to a new <see cref="RefreshToken.FamilyId" />
    ///     — call <see cref="RotateAsync" /> for subsequent
    ///     rotations within the same session.
    /// </summary>
    /// <param name="userId">Owner user id.</param>
    /// <param name="rawToken">43-char base64url raw token shown to the client.</param>
    /// <param name="expiresAtUtc">Token expiry — default 30 days from issue.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    /// <returns>The persisted record with id + familyId assigned.</returns>
    public Task<RefreshToken> IssueAsync(
        Guid userId,
        string rawToken,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Look up a token by its SHA-256 hash. Returns <c>null</c>
    ///     if no token matches.
    /// </summary>
    /// <param name="rawToken">The raw token the client just sent.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    public Task<RefreshToken?> FindByRawTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Atomically rotate a token. Sets <c>RevokedAt</c> on the
    ///     old record with <c>ReplacedBy</c> = new id; inserts the
    ///     new record in the same <c>FamilyId</c>.
    /// </summary>
    /// <param name="rawToken">The raw token being rotated.</param>
    /// <param name="newRawToken">The new raw token (43-char base64url).</param>
    /// <param name="newExpiresAtUtc">New token's expiry.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    /// <returns>The rotation outcome — see <see cref="RefreshRotationResult" />.</returns>
    public Task<RefreshRotationResult> RotateAsync(
        string rawToken,
        string newRawToken,
        DateTimeOffset newExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revoke a single token. Sets <c>RevokedAt</c> to
    ///     <see cref="DateTimeOffset.UtcNow" />; idempotent.
    /// </summary>
    /// <param name="rawToken">The raw token to revoke.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    /// <returns>
    ///     <c>true</c> if a matching active token was found and
    ///     updated; <c>false</c> if the token was unknown or
    ///     already revoked.
    /// </returns>
    public Task<bool> RevokeAsync(
        string rawToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revoke every token in a rotation family. Called when
    ///     replay is detected — every device for the same session
    ///     is signed out simultaneously.
    /// </summary>
    /// <param name="familyId">The rotation family id to nuke.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    /// <returns>Number of tokens marked revoked (or already revoked).</returns>
    public Task<int> RevokeFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Tri-state result of <see cref="IRefreshTokenStore.RotateAsync" />.
///     Auth service branches on this — Success continues, Replayed
///     triggers <see cref="IRefreshTokenStore.RevokeFamilyAsync" />,
///     NotFound returns 401.
/// </summary>
public enum RefreshRotationResult
{
    /// <summary>Rotation succeeded; caller issues a new access JWT.</summary>
    Success = 0,

    /// <summary>The token is unknown or already revoked. Returns 401.</summary>
    NotFound = 1,

    /// <summary>
    ///     The token was already rotated — replay attempt. Caller
    ///     MUST revoke the entire family and likely lock the
    ///     user account.
    /// </summary>
    Replayed = 2,
}
