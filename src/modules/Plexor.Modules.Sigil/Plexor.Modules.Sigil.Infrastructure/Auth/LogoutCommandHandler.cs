// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LogoutCommandHandler — revoke the presented refresh token.
// Idempotent. Even when the token is unknown or already revoked, this
// returns success (so callers can't probe which tokens are alive).
// Extracted from AuthCommandHandlers.cs (Sprint 3, item 2).
// ============================================================================

using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Logout — revoke the presented refresh token. Idempotent. Even
///     when the token is unknown or already revoked, this returns
///     success (so callers can't probe which tokens are alive).
/// </summary>
/// <param name="refreshTokens">Refresh token store — revoke by raw value.</param>
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
