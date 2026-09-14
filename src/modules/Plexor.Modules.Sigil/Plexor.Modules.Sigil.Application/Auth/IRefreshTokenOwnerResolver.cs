// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IRefreshTokenOwnerResolver — resolves the user (and role names) that
// own a refresh-token record. Extracted from RefreshCommandHandler so
// the security-critical rotation path can be unit-tested without a
// real PostgreSQL DbContext (the handler does the EF Core join
// behind this interface, tests mock it).
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Look up the user who owns a rotated refresh token, plus the
///     role names bound to that user. Used by
///     <c>RefreshCommandHandler</c> after a successful rotation to
///     issue a fresh access token.
/// </summary>
/// <remarks>
///     <para><b>Why this lives in Application.</b> The handler
///     depends on this contract; the EF Core implementation lives in
///     Infrastructure. Mocking this interface is how unit tests
///     exercise the handler's branching without spinning up a real
///     database.</para>
///     <para><b>Why the methods take raw ids, not entities.</b>
///     <see cref="ResolveByTokenHashAsync" /> returns the full
///     <see cref="User" /> so the handler can read OrgId for the
///     access-token claim; <see cref="LoadRoleNamesAsync" /> returns
///     plain strings to keep the abstraction minimal (the handler
///     just hands them to <c>ITokenIssuer</c>).</para>
/// </remarks>
public interface IRefreshTokenOwnerResolver
{
    /// <summary>
    ///     Look up the user whose refresh-token row carries the
    ///     given SHA-256 hash. Returns <c>null</c> if the token
    ///     hash doesn't match any row (e.g. the user was deleted
    ///     after the token was issued) — the handler surfaces that
    ///     as a generic InvalidCredentials to avoid leaking whether
    ///     the user ever existed.
    /// </summary>
    /// <param name="tokenHash">
    ///     The SHA-256 hash of the freshly-rotated refresh token's
    ///     raw bytes, produced by <c>RefreshTokenHasher.Hash</c>.
    /// </param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    public Task<User?> ResolveByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Load the role names bound to the given user. Walks
    ///     role_bindings → roles. Returns an empty collection when
    ///     the user has no bindings.
    /// </summary>
    /// <param name="userId">The user whose role names to load.</param>
    /// <param name="cancellationToken">Forwarded to the database.</param>
    public Task<IReadOnlyCollection<string>> LoadRoleNamesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
