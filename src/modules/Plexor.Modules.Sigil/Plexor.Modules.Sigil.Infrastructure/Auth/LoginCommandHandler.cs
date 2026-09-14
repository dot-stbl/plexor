// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoginCommandHandler — password-grant login. Validates credentials,
// applies lockout + active-account guards, increments failed-login
// counters, and issues a fresh access + refresh pair on success.
// Extracted from AuthCommandHandlers.cs (Sprint 3, item 2).
// ============================================================================

using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Authorization;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Refresh-token lifetime on login. Mirrors the lifetime baked
///     into the rotation chain. Exposed so <see cref="RefreshCommandHandler" />
///     can stamp the rotated refresh token with the same duration
///     without re-deriving it from configuration.
/// </summary>
public static class LoginRefreshTokenLifetime
{
    /// <summary>30-day refresh token lifetime.</summary>
    public static readonly TimeSpan Value = TimeSpan.FromDays(30);
}

/// <summary>
///     Password-grant login. Validates credentials, applies lockout
///     state, increments failed-login counters, and issues a fresh
///     access + refresh pair on success.
///
///     Sprint 3 (item 1): wall-clock now read via injected
///     <see cref="TimeProvider" /> per time-and-wire-format.md §3
///     (lockout counter / expiry math).
/// </summary>
/// <param name="users">User lookup — by email or username.</param>
/// <param name="passwordHasher">Password verification.</param>
/// <param name="refreshTokens">Refresh token store.</param>
/// <param name="roleNames">Role name loader for the user's bindings.</param>
/// <param name="tokenIssuer">Access token issuer.</param>
/// <param name="accountStateGuard">Lockout / counter policy.</param>
/// <param name="clock">Wall-clock — used to stamp the refresh-token expiry.</param>
public sealed class LoginCommandHandler(
    IUserLookup users,
    IPasswordHasher passwordHasher,
    IRefreshTokenStore refreshTokens,
    IRoleNameLoader roleNames,
    ITokenIssuer tokenIssuer,
    IAccountStateGuard accountStateGuard,
    TimeProvider clock) : ICommandHandler<LoginCommand, LoginResult>
{
    /// <inheritdoc />
    public async Task<LoginResult> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(command.Password))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Password is required.");
        }

        var user = await ResolveUserAsync(users, command, cancellationToken)
            ?? throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Email or username not found.");

        await accountStateGuard.EnsureNotLockedAsync(user, cancellationToken);
        ActiveAccountGuard.EnsureActive(user);
        ActiveAccountGuard.EnsurePasswordExists(user);

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash!.ToString(),
            command.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            await accountStateGuard.RegisterFailedLoginAsync(user.Id, cancellationToken);
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Invalid credentials.");
        }

        await accountStateGuard.RegisterSuccessfulLoginAsync(user.Id, cancellationToken);

        var roles = await roleNames.LoadAsync(user.Id, cancellationToken);

        // First-login password rotation: issue a short-lived token
        // whose only permission is iam.users.change-own-password, and
        // hand back NO refresh token. Until PasswordChangedAt is
        // stamped (via POST /iam/users/{userId}/password), no other
        // endpoint will admit the caller's bearer.
        if (user.PasswordChangedAt is null)
        {
            var passwordChangeAccess = await tokenIssuer.IssueWithOverrideAsync(
                user.Id,
                user.OrgId,
                roles,
                [PlexorPermissions.UsersChangeOwnPassword],
                IJwtSigningService.PasswordChangeLifetime,
                cancellationToken);

            return new LoginResult(
                AccessToken: passwordChangeAccess.CompactJwt,
                RefreshToken: string.Empty,
                AccessTokenExpiresAtUtc: passwordChangeAccess.ExpiresAtUtc);
        }

        var refreshRaw = TokenGenerator.Generate();
        var refreshExpires = clock.GetUtcNow() + LoginRefreshTokenLifetime.Value;
        await refreshTokens.IssueAsync(
            user.Id, refreshRaw, refreshExpires, cancellationToken);

        var access = await tokenIssuer.IssueAsync(
            user.Id, user.OrgId, roles, cancellationToken);

        return new LoginResult(
            AccessToken: access.CompactJwt,
            RefreshToken: refreshRaw,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc);
    }

    private static async Task<User?> ResolveUserAsync(
        IUserLookup users,
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
}