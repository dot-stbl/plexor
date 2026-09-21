// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoginCommandHandler — password-grant login. Validates credentials,
// applies lockout state, increments failed-login counters, and issues a
// fresh access + refresh pair on success.
//
// Extracted from AuthCommandHandlers.cs (issue #81 / M2) so each CQRS
// command handler lives in its own file per
// folder-organization.md §1. Orchestration only — the lockout math,
// failed/successful-login state writes, and the role projection live
// in AuthCommandHelpers.
// ============================================================================

using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Authorization;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Shared.Kernel.Identity;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Flows;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Plexor.Shared.Contracts.Routes;

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
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// lockout-expiry + refresh-token-expiry stamps (per
/// <c>time-and-wire-format.md</c> §3).</param>
/// <param name="orgAuthConfigReader">Per-tenant IDP configuration
/// reader. Read-only seam used to short-circuit email+password
/// attempts against OIDC-configured tenants (Phase 4.6.3c).</param>
public sealed class LoginCommandHandler(
    IUserLookup users,
    IPasswordHasher passwordHasher,
    IRefreshTokenStore refreshTokens,
    ITokenIssuer tokenIssuer,
    IdentityDbContext db,
    TimeProvider clock,
    IOrgAuthProviderConfigReader orgAuthConfigReader) : ICommandHandler<LoginCommand, LoginResult>
{
    /// <summary>Refresh-token lifetime on login. Kept as a public
    /// surface so <see cref="AuthCommandHelpers.RefreshTokenLifetime" /> can
    /// mirror the same value without taking a dependency on the
    /// handler class.</summary>
    internal static readonly TimeSpan RefreshTokenLifetime =
        AuthCommandHelpers.RefreshTokenLifetime;

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

        // Phase 4.6.3c — IDP guard. A tenant whose OrgAuthProviderConfig
        // row declares Provider=Oidc must NOT accept email+password
        // logins. Returning a 400 with a `redirect` extension lets
        // the console forward the operator through
        // POST /auth/oidc/authorize without leaking which orgs use
        // which backend to a probing client. Absent config row
        // (fresh deploy, in-flight seeder race) → treat as Sigil so
        // the v0.1 single-tenant default continues to work.
        var orgConfig = await orgAuthConfigReader.GetForOrgAsync(
            command.OrgId, cancellationToken);
        if (orgConfig is { Provider: OrgAuthProvider.Oidc })
        {
            var redirectPath = string.IsNullOrWhiteSpace(command.RedirectPath)
                ? "/console"
                : command.RedirectPath;
            var redirectUrl =
                $"/{ApiRoutes.Base}/auth/oidc/authorize"
                + $"?org={Uri.EscapeDataString(command.OrgId.ToString())}"
                + $"&redirect={Uri.EscapeDataString(redirectPath)}";

            throw new IdentityException(
                IdentityExceptions.CredentialsProviderMismatch,
                "This organization uses external OIDC for authentication. "
                + "Redirect to /auth/oidc/authorize to continue.",
                new Dictionary<string, object?>
                {
                    ["redirect"] = redirectUrl,
                    ["code"] = IdentityExceptions.CredentialsProviderMismatch,
                    ["title"] = "Wrong authentication method",
                });
        }

        var user = await ResolveUserAsync(command, cancellationToken) ?? throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Email or username not found.");
        await AuthCommandHelpers.EnsureNotLockedAsync(db, user, clock, cancellationToken);
        AuthCommandHelpers.EnsureActive(user);
        AuthCommandHelpers.EnsurePasswordExists(user);

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash!.ToString(),
            command.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            await AuthCommandHelpers.RegisterFailedLoginAsync(db, user.Id, clock, cancellationToken);
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Invalid credentials.");
        }

        await AuthCommandHelpers.RegisterSuccessfulLoginAsync(db, user.Id, clock, cancellationToken);

        var roles = await AuthCommandHelpers.LoadRolesAsync(db, user.Id, cancellationToken);

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
        var refreshExpires = clock.GetUtcNow() + RefreshTokenLifetime;
        await refreshTokens.IssueAsync(
            user.Id, refreshRaw, refreshExpires, cancellationToken);

        var access = await tokenIssuer.IssueAsync(
            user.Id, user.OrgId, roles, cancellationToken);

        return new LoginResult(
            AccessToken: access.CompactJwt,
            RefreshToken: refreshRaw,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc);
    }

    /// <summary>
    ///     Resolve the user by email (preferred) or username. Stays
    ///     on the handler because it threads the email-vs-username
    ///     preference through <see cref="IUserLookup" />, which the
    ///     helper doesn't depend on.
    /// </summary>
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
}
