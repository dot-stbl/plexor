// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfUserLookupShould — exercises the read-side user lookups against
// an in-memory IdentityDbContext. Email matching is case-insensitive
// (Email value object lowercases on construction); username lookup
// matches the email local-part.
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Infrastructure.Users;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="EfUserLookup" />. Each test
///     seeds one or more <see cref="User" /> rows and asserts the
///     matching predicate (email, username, id) returns the expected
///     user — or <c>null</c> for misses.
/// </summary>
public sealed class EfUserLookupShould
{
    /// <summary>Verifies that <see cref="EfUserLookup.FindByEmailAsync" />
    /// returns the user when the email matches in the same org.</summary>
    [Fact(DisplayName = "Given user in org, when FindByEmailAsync with matching email, then returns user")]
    public async Task FindByEmailAsyncReturnsMatchAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var orgId = Guid.NewGuid();
        var user = await SeedUserAsync(db, orgId, "alice@example.com", "Alice");

        var lookup = new EfUserLookup(db);
        var match = await lookup.FindByEmailAsync(orgId, "alice@example.com");

        match.ShouldNotBeNull();
        match.Id.ShouldBe(user.Id);
    }

    /// <summary>Verifies that <see cref="EfUserLookup.FindByEmailAsync" />
    /// returns <c>null</c> when the email lives in a different org —
    /// email uniqueness is scoped per-tenant.</summary>
    [Fact(DisplayName = "Given user in different org, when FindByEmailAsync, then returns null")]
    public async Task FindByEmailAsyncReturnsNullForDifferentOrgAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var userOrg = Guid.NewGuid();
        var callerOrg = Guid.NewGuid();
        await SeedUserAsync(db, userOrg, "alice@example.com", "Alice");

        var lookup = new EfUserLookup(db);
        var match = await lookup.FindByEmailAsync(callerOrg, "alice@example.com");

        match.ShouldBeNull();
    }

    /// <summary>Verifies that <see cref="EfUserLookup.FindByUsernameAsync" />
    /// matches by the email local-part — usernames and emails are
    /// 1:1 (the local-part IS the username).</summary>
    [Fact(DisplayName = "Given user with email alice@example.com, when FindByUsernameAsync('alice'), then returns user")]
    public async Task FindByUsernameAsyncMatchesByLocalPartAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var orgId = Guid.NewGuid();
        var user = await SeedUserAsync(db, orgId, "bob@example.com", "Bob");

        var lookup = new EfUserLookup(db);
        var match = await lookup.FindByUsernameAsync(orgId, "bob");

        match.ShouldNotBeNull();
        match.Id.ShouldBe(user.Id);
    }

    /// <summary>Verifies that <see cref="EfUserLookup.FindByIdAsync" />
    /// returns the user with the matching id, or <c>null</c>
    /// otherwise.</summary>
    [Fact(DisplayName = "Given user id, when FindByIdAsync, then returns user; when id is unknown, returns null")]
    public async Task FindByIdAsyncReturnsMatchOrNullAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var user = await SeedUserAsync(db, Guid.NewGuid(), "carol@example.com", "Carol");

        var lookup = new EfUserLookup(db);
        var match = await lookup.FindByIdAsync(user.Id);
        var miss = await lookup.FindByIdAsync(Guid.NewGuid());

        match.ShouldNotBeNull();
        match.Id.ShouldBe(user.Id);
        miss.ShouldBeNull();
    }

    private static async Task<User> SeedUserAsync(
        IdentityDbContext db,
        Guid orgId,
        string email,
        string displayName)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Email = new Email(email),
            DisplayName = displayName,
            Status = "active",
            PasswordHash = null,
            FailedLoginCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        return user;
    }
}
