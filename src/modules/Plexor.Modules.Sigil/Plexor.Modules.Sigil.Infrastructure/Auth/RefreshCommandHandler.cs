// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshCommandHandler — refresh-token rotation. Verifies the presented
// token, rotates it inside the same family, re-issues the access token
// against the resolved permissions, and triggers family revocation on
// replay.
//
// Extracted from AuthCommandHandlers.cs (issue #81 / M2) so each CQRS
// command handler lives in its own file per
// folder-organization.md §1. Orchestration only — the rotation + owner
// resolution primitives live in AuthCommandHelpers.
// ============================================================================

using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Shared.Kernel.Identity;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Flows;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

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
