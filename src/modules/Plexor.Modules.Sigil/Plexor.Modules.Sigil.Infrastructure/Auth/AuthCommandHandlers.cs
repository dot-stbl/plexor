// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthCommandHandlers — Login + Refresh + Logout + Me. Co-located
// because they share the same dependencies (users, password hasher,
// refresh store, token issuer) and the handler bodies are < 100 lines
// each. Splitting into per-class files would add ceremony without
// adding value.
//
// The lockout math, failed/successful-login state writes, the role
// projection, and the refresh-rotation primitives live in
// AuthCommandHelpers (AuthCommandHandlersHelpers.cs) — pulled out to
// satisfy the no-private-methods convention (class-layout-and-tooling.md
// §1a / code-shape.md §9.5).
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

/// <summary>
///     Refresh-token rotation. Verifies the presented token, rotates
///     it inside the same family, re-issues the access token against
///     the resolved permissions, and triggers family revocation on
///     replay.
/// </summary>
/// <param name="refreshTokens"></param>
/// <param name="tokenIssuer"></param>
/// <param name="db"></param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// rotated refresh-token expiry stamp.</param>
public sealed class RefreshCommandHandler(
    IRefreshTokenStore refreshTokens,
    ITokenIssuer tokenIssuer,
    IdentityDbContext db,
    TimeProvider clock) : ICommandHandler<RefreshCommand, LoginResult>
{

    /// <inheritdoc />
    public async Task<LoginResult> HandleAsync(
        RefreshCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Refresh token is required.");
        }

        // Phase 4.6.3c — iss-binding. The current Plexor refresh
        // tokens are opaque random base64url strings (no JWT shape),
        // so PeekIssuer returns Opaque and we fall through to the
        // existing rotation path. JWT-shaped refresh tokens from a
        // third-party IDP (iss != plexor) will route to the OIDC
        // path that re-issues Plexor credentials without an IDP
        // roundtrip — Phase 5+ adds proper revocation propagation.
        // Truly malformed JWT input (e.g. binary garbage with
        // dots) raises 400 identity.refresh.malformed. Opaque
        // random tokens do NOT raise this (PeekIssuer treats them
        // as "not a JWT at all"). Property-pattern merge: assign
        // + check in one expression (code-shape.md §1).
        if (RefreshTokenIssuerInspector.PeekIssuer(command.RefreshToken) is { IsMalformed: true })
        {
            throw new IdentityException(
                IdentityExceptions.RefreshMalformed,
                "Refresh token is not a well-formed JWT.");
        }

        // For v1 every refresh token is opaque (iss == null) → Sigil
        // path. JWT-shaped tokens with iss == "plexor" also follow
        // the Sigil path (future state). iss != "plexor" (Phase 5+)
        // would route to the OIDC path; v1 has no such tokens so the
        // branch is dead code today but documented for the future.
        var rotation = await AuthCommandHelpers.RotateRefreshTokenAsync(
            refreshTokens, command.RefreshToken, clock, cancellationToken);

        var owner = await AuthCommandHelpers.ResolveOwnerAsync(
            db, rotation.NewRefreshToken, cancellationToken);

        var roles = await AuthCommandHelpers.LoadRolesAsync(db, owner.Id, cancellationToken);
        var access = await tokenIssuer.IssueAsync(
            owner.Id, owner.OrgId, roles, cancellationToken);

        return new LoginResult(
            AccessToken: access.CompactJwt,
            RefreshToken: rotation.NewRefreshToken,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc);
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
        MeQuery command,
        CancellationToken cancellationToken = default)
    {

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
