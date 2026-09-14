// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ChangePasswordCommandHandlerShould — exercises the password-rotation
// flow. Verifies the current password, overwrites the stored hash,
// stamps PasswordChangedAt, and revokes the user's refresh-token
// families so a stolen session is invalidated atomically.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Infrastructure.Users;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="ChangePasswordCommandHandler" />.
///     Each test seeds a <see cref="User" /> with a known
///     password + an optional refresh-token family, runs the handler,
///     and asserts the rotation contract.
/// </summary>
public sealed class ChangePasswordCommandHandlerShould
{
    private const string CurrentPassword = "correct-horse-battery-staple";
    private const string NewPassword = "rotated-password-1234";

    /// <summary>Verifies that supplying the correct current password
    /// rotates the stored hash, stamps <c>PasswordChangedAt</c>, and
    /// returns the user's id (revocation count is informational).</summary>
    [Fact(DisplayName = "Given matching current password, when HandleAsync, then new hash stored and PasswordChangedAt stamped")]
    public async Task ValidCurrentPasswordRotatesHashAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var hasher = new PlexorPasswordHasher(new Microsoft.AspNetCore.Identity.PasswordHasher<User>());
        var store = new EfRefreshTokenStore(db);
        var user = await SeedUserWithPasswordAsync(db, hasher, CurrentPassword);
        var sut = new ChangePasswordCommandHandler(db, hasher, store);

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(user.Id, CurrentPassword, NewPassword));

        result.UserId.ShouldBe(user.Id);
        db.ChangeTracker.Clear();
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed.ShouldNotBeNull();
        refreshed.PasswordHash.ShouldNotBeNull();
        refreshed.PasswordHash.ToString().ShouldNotBe(hasher.HashPassword(user, CurrentPassword));
        refreshed.PasswordChangedAt.ShouldNotBeNull();
        refreshed.PasswordChangedAt.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    /// <summary>Verifies that supplying the wrong current password
    /// throws <see cref="IdentityException" /> with the
    /// <c>identity.credentials.invalid</c> code — the same code
    /// used when the user doesn't exist (account-enumeration
    /// mitigation).</summary>
    [Fact(DisplayName = "Given wrong current password, when HandleAsync, then throws IdentityException with InvalidCredentials")]
    public async Task WrongCurrentPasswordThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var hasher = new PlexorPasswordHasher(new Microsoft.AspNetCore.Identity.PasswordHasher<User>());
        var store = new EfRefreshTokenStore(db);
        var user = await SeedUserWithPasswordAsync(db, hasher, CurrentPassword);
        var sut = new ChangePasswordCommandHandler(db, hasher, store);

        var ex = await Should.ThrowAsync<IdentityException>(
            () => sut.HandleAsync(
                new ChangePasswordCommand(user.Id, "totally-wrong-password", NewPassword)));

        ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);
    }

    /// <summary>Verifies that a new password shorter than 8
    /// characters fails before any DB write happens — the
    /// <c>InvalidPasswordHash</c> code bubbles up.</summary>
    [Fact(DisplayName = "Given short new password, when HandleAsync, then throws IdentityException with InvalidPasswordHash")]
    public async Task ShortNewPasswordThrowsAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var hasher = new PlexorPasswordHasher(new Microsoft.AspNetCore.Identity.PasswordHasher<User>());
        var store = new EfRefreshTokenStore(db);
        var user = await SeedUserWithPasswordAsync(db, hasher, CurrentPassword);
        var sut = new ChangePasswordCommandHandler(db, hasher, store);

        var ex = await Should.ThrowAsync<IdentityException>(
            () => sut.HandleAsync(
                new ChangePasswordCommand(user.Id, CurrentPassword, "short")));

        ex.Code.ShouldBe(IdentityExceptions.InvalidPasswordHash);
    }

    /// <summary>Verifies that a successful rotation revokes every
    /// refresh-token family the user owns — the atomic
    /// "log out every session" side effect.</summary>
    [Fact(DisplayName = "Given user with active refresh-token families, when HandleAsync, then families are revoked")]
    public async Task RotationRevokesRefreshTokenFamiliesAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var hasher = new PlexorPasswordHasher(new Microsoft.AspNetCore.Identity.PasswordHasher<User>());
        var store = new EfRefreshTokenStore(db);
        var user = await SeedUserWithPasswordAsync(db, hasher, CurrentPassword);
        // Seed two active families for this user.
        await store.IssueAsync(user.Id, "raw-token-aaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow.AddDays(30));
        await store.IssueAsync(user.Id, "raw-token-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", DateTimeOffset.UtcNow.AddDays(30));
        var sut = new ChangePasswordCommandHandler(db, hasher, store);

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(user.Id, CurrentPassword, NewPassword));

        result.RefreshTokensRevoked.ShouldBe(2);
        db.ChangeTracker.Clear();
        var tokens = await db.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == user.Id)
            .ToListAsync();
        tokens.ShouldAllBe(token => token.RevokedAt != null);
    }

    private static async Task<User> SeedUserWithPasswordAsync(
        IdentityDbContext db,
        PlexorPasswordHasher hasher,
        string currentPassword)
    {
        var now = DateTimeOffset.UtcNow;
        // Build the user in two saves: first insert with a
        // sentinel hash, then update with the real one. PasswordHash
        // is init-only so we can't set it after construction, but
        // EF's change tracker can replace the property value via
        // ExecuteUpdate for the test scaffold.
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            Email = new Email("alice@example.com"),
            DisplayName = "Alice",
            Status = "active",
            PasswordHash = new PasswordHash("AQAAAAAAAACkAAAA"),
            FailedLoginCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.PasswordHash,
                    new PasswordHash(hasher.HashPassword(user, currentPassword))));
        db.ChangeTracker.Clear();
        return await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
    }
}
