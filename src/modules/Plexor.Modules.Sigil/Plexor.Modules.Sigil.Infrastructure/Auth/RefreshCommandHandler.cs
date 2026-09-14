// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshCommandHandler — refresh-token rotation. Verifies the
// presented token, rotates it inside the same family, re-issues the
// access token against the resolved permissions, and triggers family
// revocation on replay. Extracted from AuthCommandHandlers.cs
// (Sprint 3, item 2).
// ============================================================================

using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain;
using Plexor.Modules.Sigil.Domain.Errors;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Refresh-token rotation. Verifies the presented token, rotates
///     it inside the same family, re-issues the access token against
///     the resolved permissions, and triggers family revocation on
///     replay.
/// </summary>
/// <param name="refreshTokens">Refresh token store — issue / rotate / revoke.</param>
/// <param name="tokenIssuer">Access token issuer.</param>
/// <param name="ownerResolver">
///     Resolves the user that owns the rotated refresh token, plus
///     the user's role names. Behind an interface so the handler
///     stays unit-testable without a real DbContext.
/// </param>
public sealed class RefreshCommandHandler(
    IRefreshTokenStore refreshTokens,
    ITokenIssuer tokenIssuer,
    IRefreshTokenOwnerResolver ownerResolver) : ICommandHandler<RefreshCommand, LoginResult>
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

        var newRefreshRaw = TokenGenerator.Generate();
        var newRefreshExpires = DateTimeOffset.UtcNow + LoginRefreshTokenLifetime.Value;

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

            case RefreshRotationResult.Expired:
                // Token is past its expiry — caller must log in again.
                // We do NOT revoke the family here: an expired token
                // is a normal lifecycle event, not a compromise signal.
                throw new IdentityException(
                    IdentityExceptions.RefreshTokenExpired,
                    "Refresh token has expired.");

            case RefreshRotationResult.Success:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown RefreshRotationResult: {rotation}");
        }

        // Resolve the user that owns the rotated chain. If the user
        // has been deleted between issue and refresh the resolver
        // returns null — surface as InvalidCredentials so the client
        // gets the same generic 401 as for any other auth failure
        // (no information leakage about whether the user existed).
        var owner = await ownerResolver.ResolveByTokenHashAsync(
            RefreshTokenHasher.Hash(newRefreshRaw), cancellationToken)
            ?? throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Refresh token owner not found.");

        var roles = await ownerResolver.LoadRoleNamesAsync(
            owner.Id, cancellationToken);
        var access = await tokenIssuer.IssueAsync(
            owner.Id, owner.OrgId, roles, cancellationToken);

        return new LoginResult(
            AccessToken: access.CompactJwt,
            RefreshToken: newRefreshRaw,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc);
    }
}