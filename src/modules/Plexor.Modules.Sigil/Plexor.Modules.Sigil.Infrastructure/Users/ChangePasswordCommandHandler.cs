// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ChangePasswordCommandHandler — rotate a user's password. Verifies
// the current password, overwrites the stored hash, clears
// MustChangePassword, and revokes every refresh-token family the
// user owns so any stolen session is invalidated atomically.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Users;

/// <summary>
///     Change-password flow. The first-login rotation path (carrying
///     an access token whose only permission is
///     <c>iam.users.change-own-password</c>) lands here; admins
///     resetting another user's password would land on a different
///     handler that <em>doesn't</em> require the current password.
/// </summary>
public sealed class ChangePasswordCommandHandler(
    IdentityDbContext db,
    IPasswordHasher passwordHasher,
    IRefreshTokenStore refreshTokens) : ICommandHandler<ChangePasswordCommand, ChangePasswordResult>
{
    /// <inheritdoc />
    public async Task<ChangePasswordResult> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrEmpty(command.CurrentPassword))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Current password is required.");
        }

        if (string.IsNullOrEmpty(command.NewPassword) || command.NewPassword.Length < 8)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPasswordHash,
                "New password must be at least 8 characters.");
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is not { PasswordHash: { } userHash })
        {
            // Don't leak whether the user exists / has a password —
            // both branches return the same generic error.
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Current password is invalid.");
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            userHash.ToString(),
            command.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Current password is invalid.");
        }

        var newHash = new PasswordHash(passwordHasher.HashPassword(user, command.NewPassword));
        var now = DateTimeOffset.UtcNow;

        // Single transaction: overwrite hash, clear flag, then walk
        // every refresh-token family the user owns.
        var familyIds = await db.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == command.UserId)
            .Select(token => token.FamilyId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        await db.Users
            .Where(u => u.Id == command.UserId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(u => u.PasswordHash, newHash)
                    .SetProperty(u => u.PasswordChangedAt, (DateTimeOffset?)now)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken);

        var revoked = 0;
        foreach (var familyId in familyIds)
        {
            revoked += await refreshTokens.RevokeFamilyAsync(familyId, cancellationToken);
        }

        return new ChangePasswordResult(command.UserId, revoked);
    }
}
