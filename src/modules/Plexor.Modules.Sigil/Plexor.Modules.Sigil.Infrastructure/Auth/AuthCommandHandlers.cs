// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthCommandHandlers — Login + Refresh + Logout + Me. Co-located
// because they share the same dependencies (users, password hasher,
// refresh store, token issuer) and the handler bodies are < 100 lines
// each. Splitting into per-class files would add ceremony without
// adding value.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Authorization;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Password-grant login. Validates credentials, applies lockout
///     state, increments failed-login counters, and issues a fresh
///     access + refresh pair on success.
/// </summary>
/// <param name="users"></param>
/// <param name="passwordHasher"></param>
/// <param name="refreshTokens"></param>
/// <param name="tokenIssuer"></param>
/// <param name="db"></param>
public sealed class LoginCommandHandler(
    IUserLookup users,
    IPasswordHasher passwordHasher,
    IRefreshTokenStore refreshTokens,
    ITokenIssuer tokenIssuer,
    IdentityDbContext db) : ICommandHandler<LoginCommand, LoginResult>
{
    /// <summary>Lockout threshold — failed attempts before the account
    /// is locked for <see cref="LockoutDuration" />.</summary>
    private const int FailedLoginLockoutThreshold = 5;

    /// <summary>Lockout window — account is locked for this long after
    /// the threshold is reached.</summary>
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Refresh-token lifetime on login. Mirrors the lifetime
    /// baked into the rotation chain.</summary>
    internal static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <inheritdoc />
    public async Task<LoginResult> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrEmpty(command.Password))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Password is required.");
        }

        var user = await ResolveUserAsync(command, cancellationToken) ?? throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Email or username not found.");
        await EnsureNotLockedAsync(user, cancellationToken);
        EnsureActiveAsync(user);
        EnsurePasswordExistsAsync(user);

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash!.ToString(),
            command.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            await RegisterFailedLoginAsync(user.Id, cancellationToken);
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Invalid credentials.");
        }

        await RegisterSuccessfulLoginAsync(user.Id, cancellationToken);

        var roles = await LoadRolesAsync(user.Id, cancellationToken);

        // First-login password rotation: issue a short-lived token
        // whose only permission is iam.users.change-own-password, and
        // hand back NO refresh token. Until MustChangePassword is
        // cleared via POST /iam/users/{userId}/password, no other
        // endpoint will admit the caller's bearer.
        if (user.MustChangePassword)
        {
            var passwordChangeAccess = await tokenIssuer.IssueWithOverrideAsync(
                user.Id,
                user.OrgId,
                roles,
                new[] { PlexorPermissions.UsersChangeOwnPassword },
                IJwtSigningService.PasswordChangeLifetime,
                cancellationToken);

            return new LoginResult(
                AccessToken: passwordChangeAccess.CompactJwt,
                RefreshToken: string.Empty,
                AccessTokenExpiresAtUtc: passwordChangeAccess.ExpiresAtUtc);
        }

        var refreshRaw = TokenGenerator.Generate();
        var refreshExpires = DateTimeOffset.UtcNow + RefreshTokenLifetime;
        await refreshTokens.IssueAsync(
            user.Id, refreshRaw, refreshExpires, cancellationToken);

        var access = await tokenIssuer.IssueAsync(
            user.Id, user.OrgId, roles, cancellationToken);

        return new LoginResult(
            AccessToken: access.CompactJwt,
            RefreshToken: refreshRaw,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc);
    }

    private async Task<User?> ResolveUserAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            return await users.FindByEmailAsync(command.OrgId, command.Email, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(command.Username))
        {
            return await users.FindByUsernameAsync(command.OrgId, command.Username, cancellationToken);
        }

        throw new IdentityException(
            IdentityExceptions.InvalidCredentials,
            "Either email or username must be supplied.");
    }

    private static void EnsureActiveAsync(User user)
    {
        if (!string.Equals(user.Status, "active", StringComparison.Ordinal))
        {
            throw new IdentityException(
                IdentityExceptions.AccountSuspended,
                "Account is not active.");
        }
    }

    private static void EnsurePasswordExistsAsync(User user)
    {
        if (user.PasswordHash is null)
        {
            // OAuth-only user attempting password login — surface as
            // generic invalid-credentials so we don't leak the auth
            // mode.
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Password login not available for this account.");
        }
    }

    private async Task EnsureNotLockedAsync(
        User user,
        CancellationToken cancellationToken)
    {
        if (user.LockedUntil is { } until && until > DateTimeOffset.UtcNow)
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

    private async Task RegisterFailedLoginAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1),
                cancellationToken);

        // Threshold check: if the new count crossed the threshold,
        // stamp the lockout expiry. Done in a second update because
        // ExecuteUpdate with conditional logic is awkward; the
        // racing window is small (lockout granularity is minutes).
        var current = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FailedLoginCount)
            .FirstAsync(cancellationToken);

        if (current >= FailedLoginLockoutThreshold)
        {
            var lockoutUntil = DateTimeOffset.UtcNow + LockoutDuration;
            await db.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(u => u.LockedUntil, (DateTimeOffset?)lockoutUntil),
                    cancellationToken);
        }
    }

    private async Task RegisterSuccessfulLoginAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.FailedLoginCount, 0)
                    .SetProperty(u => u.LockedUntil, (DateTimeOffset?)null)
                    .SetProperty(u => u.LastLoginAt, (DateTimeOffset?)now),
                cancellationToken);
    }

    private async Task<IReadOnlyCollection<string>> LoadRolesAsync(
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
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
///     Refresh-token rotation. Verifies the presented token, rotates
///     it inside the same family, re-issues the access token against
///     the resolved permissions, and triggers family revocation on
///     replay.
/// </summary>
/// <param name="refreshTokens"></param>
/// <param name="tokenIssuer"></param>
/// <param name="db"></param>
public sealed class RefreshCommandHandler(
    IRefreshTokenStore refreshTokens,
    ITokenIssuer tokenIssuer,
    IdentityDbContext db) : ICommandHandler<RefreshCommand, LoginResult>
{
    /// <inheritdoc />
    public async Task<LoginResult> HandleAsync(
        RefreshCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Refresh token is required.");
        }

        var newRefreshRaw = TokenGenerator.Generate();
        var newRefreshExpires = DateTimeOffset.UtcNow + LoginCommandHandler.RefreshTokenLifetime;

        var rotation = await refreshTokens.RotateAsync(
            command.RefreshToken,
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
                    command.RefreshToken, cancellationToken);
                if (replayed is not null)
                {
                    await refreshTokens.RevokeFamilyAsync(
                        replayed.FamilyId, cancellationToken);
                }
                throw new IdentityException(
                    IdentityExceptions.RefreshTokenReplayed,
                    "Refresh token replay detected; family revoked.");

            case RefreshRotationResult.Success:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown RefreshRotationResult: {rotation}");
        }

        // Load the user that owns this chain (the rotated entity's
        // userId is preserved on the new record).
        var owner = await db.RefreshTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == RefreshTokenHasher.Hash(newRefreshRaw))
            .Join(db.Users, token => token.UserId, user => user.Id, (_, user) => user)
            .FirstAsync(cancellationToken);

        var roles = await LoadRolesAsync(owner.Id, cancellationToken);
        var access = await tokenIssuer.IssueAsync(
            owner.Id, owner.OrgId, roles, cancellationToken);

        return new LoginResult(
            AccessToken: access.CompactJwt,
            RefreshToken: newRefreshRaw,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc);
    }

    private async Task<IReadOnlyCollection<string>> LoadRolesAsync(
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
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
///     Logout — revoke the presented refresh token. Idempotent. Even
///     when the token is unknown or already revoked, this returns
///     success (so callers can't probe which tokens are alive).
/// </summary>
/// <param name="refreshTokens"></param>
public sealed class LogoutCommandHandler(
    IRefreshTokenStore refreshTokens) : ICommandHandler<LogoutCommand, LogoutResult>
{
    /// <inheritdoc />
    public async Task<LogoutResult> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return new LogoutResult(RevokedTokens: 0);
        }

        await refreshTokens.RevokeAsync(command.RefreshToken, cancellationToken);
        return new LogoutResult(RevokedTokens: 1);
    }
}

/// <summary>
///     Me — return the authenticated caller's identity, roles, and
///     permissions as resolved by the bearer handler. Reads through
///     <see cref="ICurrentUser" />; never touches the DB on the hot
///     path (all values come from the JWT claims).
/// </summary>
/// <param name="currentUser"></param>
public sealed class MeQueryHandler(
    ICurrentUser currentUser) : ICommandHandler<MeQuery, MeResult>
{
    /// <inheritdoc />
    public Task<MeResult> HandleAsync(
        MeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (currentUser.UserId == Guid.Empty)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Caller is not authenticated.");
        }

        return Task.FromResult(new MeResult(
            UserId: currentUser.UserId,
            OrgId: currentUser.TenantId,
            Roles: currentUser.Roles,
            Permissions: currentUser.Permissions));
    }
}

/// <summary>
///     Marker interface shared by every auth command/query handler. The
///     mediator-style dispatch lives in Phase 5; for now callers
///     invoke handlers directly.
/// </summary>
/// <typeparam name="TCommand"></typeparam>
/// <typeparam name="TResult"></typeparam>
public interface ICommandHandler<TCommand, TResult>
{
    /// <summary>Handle the command and return its result.</summary>
    /// <param name="command">The inbound command payload.</param>
    /// <param name="cancellationToken">Forwarded to IO.</param>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
