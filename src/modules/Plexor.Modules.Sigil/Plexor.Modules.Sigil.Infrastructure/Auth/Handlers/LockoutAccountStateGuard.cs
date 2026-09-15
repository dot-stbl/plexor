// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LockoutAccountStateGuard — production IAccountStateGuard impl.
// Stamps lockout expiry on the user row once the failed counter
// crosses the configured threshold. Counter + window are constants
// here (kept on the implementation, not the interface, so tests can
// substitute a tighter / looser policy).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Production <see cref="IAccountStateGuard" />: 5 failed logins
///     locks the account for 15 minutes. Constants live here (not on
///     the interface) so test substitutes can use tighter thresholds.
/// </summary>
/// <param name="db">IdentityDbContext — the backing store for user rows.</param>
/// <param name="clock">Wall-clock for "now" — injected for testability.</param>
public sealed class LockoutAccountStateGuard(
    IdentityDbContext db,
    TimeProvider clock) : IAccountStateGuard
{
    /// <summary>Lockout threshold — failed attempts before the account is locked.</summary>
    public const int FailedLoginLockoutThreshold = 5;

    /// <summary>Lockout window — account is locked for this long after the threshold is reached.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public async Task EnsureNotLockedAsync(User user, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        if (user.LockedUntil is { } until && until > now)
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

    /// <inheritdoc />
    public async Task RegisterFailedLoginAsync(Guid userId, CancellationToken cancellationToken = default)
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
            var lockoutUntil = clock.GetUtcNow() + LockoutDuration;
            await db.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(u => u.LockedUntil, (DateTimeOffset?)lockoutUntil),
                    cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task RegisterSuccessfulLoginAsync(Guid userId, CancellationToken cancellationToken = default)
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
}
