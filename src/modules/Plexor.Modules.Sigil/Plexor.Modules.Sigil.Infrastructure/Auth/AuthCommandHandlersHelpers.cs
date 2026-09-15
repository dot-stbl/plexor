// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthCommandHelpers — file-static helpers pulled out of
// AuthCommandHandlers.cs (Login + Refresh) to satisfy the no-private-
// methods convention (class-layout-and-tooling.md §1a / code-shape.md
// §9.5). Each handler orchestrates the call sequence; the helpers own
// the lockout math, the failed/successful-login state writes, the
// role projection, and the refresh-rotation primitives.
//
// Method-level dependency injection: every helper takes only the
// dependencies it actually uses (IdentityDbContext for EF, IRefreshTokenStore
// for rotation, TimeProvider for stamps). The two handlers stay as
// thin orchestrators that hand the helper its dependencies.
//
// The duplicate LoadRolesAsync in the original LoginCommandHandler +
// RefreshCommandHandler is consolidated here — the bodies were
// identical (same projection, same join), so one canonical
// implementation is the right shape.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Flows;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Static helpers shared by <see cref="LoginCommandHandler" /> and
///     <see cref="RefreshCommandHandler" />. Pure functions over the
///     supplied dependencies + the supplied domain types — extracted
///     to satisfy the no-private-methods rule.
/// </summary>
internal static class AuthCommandHelpers
{
    /// <summary>
    ///     Lockout threshold — failed attempts before the account
    ///     is locked for <see cref="LockoutDuration" />.
    /// </summary>
    public const int FailedLoginLockoutThreshold = 5;

    /// <summary>
    ///     Lockout window — account is locked for this long after
    ///     the threshold is reached.
    /// </summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Refresh-token lifetime on login. Mirrors the lifetime
    ///     baked into the rotation chain.
    /// </summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>
    ///     Validate that the user's account is in the
    ///     <c>active</c> status. Throws <see cref="IdentityException" />
    ///     with the <see cref="IdentityExceptions.AccountSuspended" />
    ///     code otherwise.
    /// </summary>
    /// <param name="user">Resolved Plexor user.</param>
    public static void EnsureActive(User user)
    {
        if (!string.Equals(user.Status, "active", StringComparison.Ordinal))
        {
            throw new IdentityException(
                IdentityExceptions.AccountSuspended,
                "Account is not active.");
        }
    }

    /// <summary>
    ///     Validate that the user has a password set. OAuth-only
    ///     users (no <c>PasswordHash</c>) attempting email+password
    ///     login surface as generic invalid credentials so the auth
    ///     mode isn't leaked.
    /// </summary>
    /// <param name="user">Resolved Plexor user.</param>
    public static void EnsurePasswordExists(User user)
    {
        if (user.PasswordHash is null)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Password login not available for this account.");
        }
    }

    /// <summary>
    ///     Validate the lockout window. If the account is currently
    ///     locked, raise <see cref="IdentityException" /> with the
    ///     <see cref="IdentityExceptions.AccountLocked" /> code. If
    ///     the lockout window has elapsed, clear the flag so a fresh
    ///     login attempt can succeed without forcing the user to
    ///     wait.
    /// </summary>
    /// <param name="db">Scoped identity DbContext.</param>
    /// <param name="user">Resolved Plexor user.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" />.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task EnsureNotLockedAsync(
        IdentityDbContext db,
        User user,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (user.LockedUntil is { } until && until > clock.GetUtcNow())
        {
            throw new IdentityException(
                IdentityExceptions.AccountLocked,
                $"Account locked until {until:O}.");
        }

        // Lockout window elapsed — clear the flag so a fresh login
        // attempt can succeed without forcing the user to wait.
        if (user.LockedUntil is not null)
        {
            await db.Users
                .Where(u => u.Id == user.Id && u.LockedUntil != null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(u => u.LockedUntil, (DateTimeOffset?)null),
                    cancellationToken);
        }
    }

    /// <summary>
    ///     Bump the failed-login counter, and stamp the lockout
    ///     expiry when the new count crosses
    ///     <see cref="FailedLoginLockoutThreshold" />. Done in two
    ///     updates because <c>ExecuteUpdate</c> with conditional
    ///     logic is awkward; the racing window is small (lockout
    ///     granularity is minutes).
    /// </summary>
    /// <param name="db">Scoped identity DbContext.</param>
    /// <param name="userId">Owner of the failed attempt.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the
    /// lockout-expiry stamp.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task RegisterFailedLoginAsync(
        IdentityDbContext db,
        Guid userId,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1),
                cancellationToken);

        var current = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FailedLoginCount)
            .FirstAsync(cancellationToken);

        if (current >= FailedLoginLockoutThreshold)
        {
            var lockoutUntil = clock.GetUtcNow() + LockoutDuration;
            await db.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(u => u.LockedUntil, (DateTimeOffset?)lockoutUntil),
                    cancellationToken);
        }
    }

    /// <summary>
    ///     Reset the failed-login counter + lockout flag and stamp
    ///     <c>LastLoginAt</c> after a successful login.
    /// </summary>
    /// <param name="db">Scoped identity DbContext.</param>
    /// <param name="userId">Owner of the successful login.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the
    /// <c>LastLoginAt</c> stamp.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task RegisterSuccessfulLoginAsync(
        IdentityDbContext db,
        Guid userId,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.FailedLoginCount, 0)
                    .SetProperty(u => u.LockedUntil, (DateTimeOffset?)null)
                    .SetProperty(u => u.LastLoginAt, (DateTimeOffset?)now),
                cancellationToken);
    }

    /// <summary>
    ///     Project the user's role bindings to the matching
    ///     <see cref="Role.Name" /> values. Canonical implementation
    ///     shared by <see cref="LoginCommandHandler" /> and
    ///     <see cref="RefreshCommandHandler" /> (the original two
    ///     copies were identical).
    /// </summary>
    /// <param name="db">Scoped identity DbContext.</param>
    /// <param name="userId">Owner of the role bindings.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task<IReadOnlyCollection<string>> LoadRolesAsync(
        IdentityDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.RoleBindings
            .AsNoTracking()
            .Where(binding => binding.UserId == userId)
            .Join(
                db.Roles.AsNoTracking(),
                binding => binding.RoleId,
                role => role.Id,
                (_, role) => role.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    ///     Inner rotation step. Rotates the presented refresh token
    ///     inside the same family; surfaces replay / not-found as
    ///     typed exceptions. Returns the raw value of the new
    ///     refresh token so the outer handler can resolve the owner
    ///     without an extra hash roundtrip.
    /// </summary>
    /// <param name="refreshTokens">Refresh-token store.</param>
    /// <param name="presentedToken">Raw refresh token from the caller.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" /> for the
    /// rotated refresh-expiry stamp.</param>
    /// <param name="cancellationToken">Forwarded to the store.</param>
    /// <returns>The raw value of the freshly-issued refresh token.</returns>
    /// <exception cref="IdentityException">
    ///     Thrown when the rotation surfaces a typed failure
    ///     (not-found / replay).
    /// </exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<RefreshRotationOutput> RotateRefreshTokenAsync(
        IRefreshTokenStore refreshTokens,
        string presentedToken,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var newRefreshRaw = TokenGenerator.Generate();
        var newRefreshExpires = clock.GetUtcNow() + RefreshTokenLifetime;

        var rotation = await refreshTokens.RotateAsync(
            presentedToken,
            newRefreshRaw,
            newRefreshExpires,
            cancellationToken);

        switch (rotation)
        {
            case RefreshRotationResult.NotFound:
                throw new IdentityException(
                    IdentityExceptions.InvalidCredentials,
                    "Refresh token not found.");

            case RefreshRotationResult.Replayed:
                // Token was already rotated or revoked — treat as
                // compromised. Look up its family and nuke everything.
                var replayed = await refreshTokens.FindByRawTokenAsync(
                    presentedToken, cancellationToken);
                if (replayed is not null)
                {
                    await refreshTokens.RevokeFamilyAsync(
                        replayed.FamilyId, cancellationToken);
                }
                throw new IdentityException(
                    IdentityExceptions.RefreshTokenReplayed,
                    "Refresh token replay detected; family revoked.");

            case RefreshRotationResult.Success:
                return new RefreshRotationOutput(newRefreshRaw);

            default:
                throw new InvalidOperationException(
                    $"Unknown RefreshRotationResult: {rotation}");
        }
    }

    /// <summary>
    ///     Resolve the user that owns the rotated refresh chain. The
    ///     <see cref="RefreshToken.UserId" /> on the new record is
    ///     preserved across rotations inside the same family.
    /// </summary>
    /// <param name="db">Scoped identity DbContext.</param>
    /// <param name="newRefreshToken">Raw value of the new refresh
    /// token (just issued by <see cref="RotateRefreshTokenAsync" />).</param>
    /// <param name="cancellationToken">Forwarded to the read.</param>
    public static async Task<User> ResolveOwnerAsync(
        IdentityDbContext db,
        string newRefreshToken,
        CancellationToken cancellationToken)
    {
        return await db.RefreshTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == RefreshTokenHasher.Hash(newRefreshToken))
            .Join(db.Users, token => token.UserId, user => user.Id, (_, user) => user)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    ///     Internal carrier for the new refresh token's raw value so
    ///     the rotation step can hand it back to the outer handler
    ///     without leaking the rest of its return shape.
    /// </summary>
    /// <param name="NewRefreshToken">Raw base64url value of the
    /// newly-issued refresh token.</param>
    public readonly record struct RefreshRotationOutput(string NewRefreshToken);
}
