// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfUserRevocationChecker — EF Core implementation of
// IUserRevocationChecker. Reads status + password_changed_at from
// sigil.users in one round-trip.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     EF Core implementation of <see cref="IUserRevocationChecker" />.
///     AsNoTracking — the read is fire-and-forget. Returns the
///     three-state outcome in one DB roundtrip.
/// </summary>
public sealed class EfUserRevocationChecker(IdentityDbContext db) : IUserRevocationChecker
{
    /// <inheritdoc />
    public async Task<RevocationCheckResult> IsStillValidAsync(
        Guid userId,
        DateTimeOffset tokenIssuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);

        var snapshot = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserRevocationSnapshot(
                u.Status,
                u.PasswordChangedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return new RevocationCheckResult.UserDisabled("User not found.");
        }

        if (!string.Equals(snapshot.Status, "active", StringComparison.Ordinal))
        {
            return new RevocationCheckResult.UserDisabled(
                $"User status is '{snapshot.Status}'.");
        }

        if (snapshot.PasswordChangedAt is { } rotatedAt
            && rotatedAt > tokenIssuedAtUtc)
        {
            return new RevocationCheckResult.PasswordRotated(rotatedAt);
        }

        return new RevocationCheckResult.Active();
    }

    /// <summary>Internal projection — keeps the EF query to two
    /// columns instead of selecting the whole row.</summary>
    private sealed record UserRevocationSnapshot(string Status, DateTimeOffset? PasswordChangedAt);
}
