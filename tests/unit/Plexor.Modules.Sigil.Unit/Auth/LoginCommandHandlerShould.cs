// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoginCommandHandlerShould — security-critical behavioural coverage for
// the password-grant login flow. Exercises the handler against an
// in-memory IdentityDbContext (real EF Core, no real Postgres) with
// NSubstitute mocks for IPasswordHasher / ITokenIssuer /
// IRefreshTokenStore. The lookup surface (IUserLookup) is the real
// EfUserLookup because it is a thin LINQ wrapper over the DbContext.
//
// Lockout threshold + window live on the production
// `LockoutAccountStateGuard` (5 failures, 15 minutes) — tested as
// observable behaviour, not by reaching into the constants. The
// guard reads wall-clock via `TimeProvider.System`; "lockout
// expired" tests seed LockedUntil in the past rather than fake the
// clock.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Authorization;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Auth;

/// <summary>
///     Behavioural tests for <see cref="LoginCommandHandler" />.
///     Each test mints a fresh in-memory <see cref="IdentityDbContext" />
///     and a fresh set of NSubstitute mocks so failures cannot bleed
///     between tests.
/// </summary>
public sealed class LoginCommandHandlerShould
{
    private const string RightPassword = "correct-horse-battery-staple";
    private const string WrongPassword = "definitely-not-it";

    [Fact(DisplayName = "Given correct password + PasswordChangedAt set, when login, then returns access + refresh and resets login metadata")]
    public async Task LoginWithCorrectPassword_ReturnsAccessAndRefresh()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, issuer, refresh, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        var userId = await SeedUserAsync(db, users, NewUserBuilder()
            .WithOrgId(orgId)
            .WithPasswordChangedAt(DateTimeOffset.UtcNow.AddDays(-7))
            .Build());
        await SeedRoleBindingAsync(db, userId, "viewer");
        issuer.IssueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedAccessToken("access.jwt.payload", DateTimeOffset.UtcNow.AddMinutes(15)));
        refresh.IssueAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FamilyId = Guid.NewGuid(),
                TokenHash = "hash",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                CreatedAt = DateTimeOffset.UtcNow,
            });
        hasher.VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword);
        var result = await sut.HandleAsync(command);

        result.AccessToken.ShouldBe("access.jwt.payload");
        result.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        result.AccessTokenExpiresAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        var persisted = await db.Users.AsNoTracking().SingleAsync();
        persisted.FailedLoginCount.ShouldBe(0);
        persisted.LockedUntil.ShouldBeNull();
        persisted.LastLoginAt.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Given correct password but PasswordChangedAt is null, when login, then issues password-change token and no refresh")]
    public async Task LoginWithCorrectPassword_ButPasswordChangedAtIsNull_ReturnsAccessWithoutRefresh()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, issuer, refresh, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        var userId = await SeedUserAsync(db, users, NewUserBuilder()
            .WithOrgId(orgId)
            .WithPasswordChangedAt(null)
            .Build());
        await SeedRoleBindingAsync(db, userId, "viewer");
        issuer.IssueWithOverrideAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Is<IReadOnlyCollection<string>>(static perms => perms.Contains(PlexorPermissions.UsersChangeOwnPassword)),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new IssuedAccessToken("password-change.jwt.payload", DateTimeOffset.UtcNow.AddMinutes(5)));
        hasher.VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword);
        var result = await sut.HandleAsync(command);

        result.AccessToken.ShouldBe("password-change.jwt.payload");
        result.RefreshToken.ShouldBe(string.Empty);
        result.AccessTokenExpiresAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        await refresh.DidNotReceive().IssueAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await issuer.DidNotReceive().IssueAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given wrong password, when login, then increments FailedLoginCount by 1 and throws InvalidCredentials without locking")]
    public async Task LoginWithWrongPassword_IncrementsFailedCounter()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, _, _, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        await SeedUserAsync(db, users, NewUserBuilder().WithOrgId(orgId).Build());
        hasher.VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Failed);

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: WrongPassword);
        var ex = await Should.ThrowAsync<IdentityException>(() => sut.HandleAsync(command));

        ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);

        var persisted = await db.Users.AsNoTracking().SingleAsync();
        persisted.FailedLoginCount.ShouldBe(1);
        persisted.LockedUntil.ShouldBeNull();
    }

    [Fact(DisplayName = "Given 5 consecutive wrong passwords, when 6th attempt is made, then account is locked and the attempt throws AccountLocked")]
    public async Task LoginWithWrongPassword_FiveTimes_LocksAccount()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, _, _, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        await SeedUserAsync(db, users, NewUserBuilder().WithOrgId(orgId).Build());
        hasher.VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Failed);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var ex = await Should.ThrowAsync<IdentityException>(() => sut.HandleAsync(BuildCommand(orgId: orgId, email: "alice@example.com", password: WrongPassword)));
            ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);
        }

        var lockedUser = await db.Users.AsNoTracking().SingleAsync();
        lockedUser.FailedLoginCount.ShouldBe(5);
        lockedUser.LockedUntil.ShouldNotBeNull();
        lockedUser.LockedUntil!.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        var blocked = await Should.ThrowAsync<IdentityException>(() => sut.HandleAsync(BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword)));
        blocked.Code.ShouldBe(IdentityExceptions.AccountLocked);
    }

    [Fact(DisplayName = "Given user with LockedUntil in the future, when login, then throws AccountLocked without checking the password")]
    public async Task LoginToLockedAccount_ThrowsImmediately()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, _, _, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        var lockUntil = DateTimeOffset.UtcNow.AddMinutes(10);
        await SeedUserAsync(db, users, NewUserBuilder()
            .WithOrgId(orgId)
            .WithLockedUntil(lockUntil)
            .WithFailedLoginCount(5)
            .Build());

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword);
        var ex = await Should.ThrowAsync<IdentityException>(() => sut.HandleAsync(command));

        ex.Code.ShouldBe(IdentityExceptions.AccountLocked);
        hasher.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact(DisplayName = "Given user with Status=suspended, when login, then throws AccountSuspended without checking the password")]
    public async Task LoginSuspendedUser_ThrowsAccountSuspended()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, _, _, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        await SeedUserAsync(db, users, NewUserBuilder()
            .WithOrgId(orgId)
            .WithStatus("suspended")
            .Build());

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword);
        var ex = await Should.ThrowAsync<IdentityException>(() => sut.HandleAsync(command));

        ex.Code.ShouldBe(IdentityExceptions.AccountSuspended);
        hasher.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact(DisplayName = "Given user with PasswordHash=null (OAuth-only), when login with password, then throws InvalidCredentials without revealing the auth mode")]
    public async Task LoginOAuthOnlyUser_ThrowsInvalidCredentials()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, _, _, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        await SeedUserAsync(db, users, NewUserBuilder()
            .WithOrgId(orgId)
            .WithPasswordHash(null)
            .Build());

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword);
        var ex = await Should.ThrowAsync<IdentityException>(() => sut.HandleAsync(command));

        ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);
        hasher.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact(DisplayName = "Given unknown email, when login, then throws InvalidCredentials without revealing whether the account exists")]
    public async Task LoginWithUnknownEmail_ThrowsInvalidCredentials()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, _, _, _) = await BuildSutAsync(db);

        var command = BuildCommand(orgId: Guid.NewGuid(), email: "ghost@example.com", password: RightPassword);
        var ex = await Should.ThrowAsync<IdentityException>(() => sut.HandleAsync(command));

        ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);
        hasher.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact(DisplayName = "Given user with prior failed attempts (but not locked), when correct password is presented, then resets FailedLoginCount and stamps LastLoginAt")]
    public async Task LoginSuccess_AfterFailedCounter_ResetsCounter()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, issuer, refresh, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        await SeedUserAsync(db, users, NewUserBuilder()
            .WithOrgId(orgId)
            .WithFailedLoginCount(3)
            .Build());
        issuer.IssueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedAccessToken("access.jwt.payload", DateTimeOffset.UtcNow.AddMinutes(15)));
        refresh.IssueAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                FamilyId = Guid.NewGuid(),
                TokenHash = "hash",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                CreatedAt = DateTimeOffset.UtcNow,
            });
        hasher.VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword);
        var result = await sut.HandleAsync(command);

        result.AccessToken.ShouldBe("access.jwt.payload");
        result.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        var persisted = await db.Users.AsNoTracking().SingleAsync();
        persisted.FailedLoginCount.ShouldBe(0);
        persisted.LockedUntil.ShouldBeNull();
        persisted.LastLoginAt.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Given user with LockedUntil in the past, when login with correct password, then lockout flag is cleared and login succeeds")]
    public async Task LoginAfterLockoutExpires_Succeeds()
    {
        await using var db = await TestDb.CreateAsync();
        var (sut, hasher, issuer, refresh, users) = await BuildSutAsync(db);
        var orgId = Guid.NewGuid();
        await SeedUserAsync(db, users, NewUserBuilder()
            .WithOrgId(orgId)
            .WithLockedUntil(DateTimeOffset.UtcNow.AddMinutes(-1))
            .WithFailedLoginCount(5)
            .Build());
        issuer.IssueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedAccessToken("access.jwt.payload", DateTimeOffset.UtcNow.AddMinutes(15)));
        refresh.IssueAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                FamilyId = Guid.NewGuid(),
                TokenHash = "hash",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                CreatedAt = DateTimeOffset.UtcNow,
            });
        hasher.VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        var command = BuildCommand(orgId: orgId, email: "alice@example.com", password: RightPassword);
        var result = await sut.HandleAsync(command);

        result.AccessToken.ShouldBe("access.jwt.payload");
        result.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        var persisted = await db.Users.AsNoTracking().SingleAsync();
        persisted.LockedUntil.ShouldBeNull();
        persisted.FailedLoginCount.ShouldBe(0);
        persisted.LastLoginAt.ShouldNotBeNull();
    }

    private static LoginCommand BuildCommand(Guid orgId, string email, string password)
    {
        return new LoginCommand(
            OrgId: orgId,
            Email: email,
            Username: null,
            Password: password);
    }

    /// <summary>Build the SUT with NSubstitute mocks for every
    /// external dependency. The lookup surface (IUserLookup) is mocked
    /// too — the real EfUserLookup relies on provider-specific LINQ
    /// translation for value-object comparisons, which InMemory +
    /// SQLite don't agree on. The account-state guard (lockout +
    /// counter policy) is the real <see cref="LockoutAccountStateGuard" />
    /// so the lockout behaviour tests exercise the production code
    /// path (not a NSubstitute mock that bypasses EF writes). Each
    /// call mints fresh mocks so tests cannot share state. Callers
    /// configure the <paramref name="db" /> context with the seeded
    /// user via <see cref="SeedUserAsync" /> and arrange the lookup
    /// mock to return it.</summary>
    /// <param name="db"></param>
    private static async Task<(
        LoginCommandHandler Sut,
        IPasswordHasher Hasher,
        ITokenIssuer Issuer,
        IRefreshTokenStore Refresh,
        IUserLookup Users)>
        BuildSutAsync(IdentityDbContext db)
    {
        var hasher = Substitute.For<IPasswordHasher>();
        var issuer = Substitute.For<ITokenIssuer>();
        var refresh = Substitute.For<IRefreshTokenStore>();
        var users = Substitute.For<IUserLookup>();
        var roles = Substitute.For<IRoleNameLoader>();
        var accountStateGuard = new LockoutAccountStateGuard(db, TimeProvider.System);

        var sut = new LoginCommandHandler(users, hasher, refresh, roles, issuer, accountStateGuard, TimeProvider.System);
        await Task.CompletedTask;
        return (sut, hasher, issuer, refresh, users);
    }

    /// <summary>Insert the supplied <paramref name="user" /> into the
    /// backing store and arrange the IUserLookup mock to return it on
    /// <c>FindByEmailAsync</c>. The mock re-queries the DbContext on
    /// each call so it returns the current row — important because the
    /// handler stamps <c>FailedLoginCount</c> / <c>LockedUntil</c> via
    /// ExecuteUpdate between login attempts; a cached snapshot would
    /// not see those changes. The real <c>EfUserLookup</c> already
    /// does this (AsNoTracking + FirstOrDefaultAsync each call), but
    /// we cannot use it directly because InMemory + SQLite disagree on
    /// translating the value-object LINQ expression it relies on.</summary>
    /// <param name="db"></param>
    /// <param name="users"></param>
    /// <param name="user"></param>
    private static async Task<Guid> SeedUserAsync(
        IdentityDbContext db,
        IUserLookup users,
        User user)
    {
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        var userId = user.Id;
        var userOrgId = user.OrgId;
        var userEmail = user.Email.Value;
        users.FindByEmailAsync(userOrgId, userEmail, Arg.Any<CancellationToken>())
            .Returns(_ => db.Users.AsNoTracking().Single(u => u.Id == userId));
        return userId;
    }

    private static UserBuilder NewUserBuilder()
    {
        return new UserBuilder();
    }

    private static async Task SeedRoleBindingAsync(
        IdentityDbContext db,
        Guid userId,
        string roleName)
    {
        var roleOrgId = Guid.NewGuid();
        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrgId = roleOrgId,
            Name = roleName,
            Description = null,
            Permissions = [],
            BuiltIn = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var binding = new RoleBinding
        {
            Id = Guid.NewGuid(),
            OrgId = roleOrgId,
            UserId = userId,
            RoleId = role.Id,
            TeamId = null,
            FolderId = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.Roles.AddAsync(role);
        await db.RoleBindings.AddAsync(binding);
        await db.SaveChangesAsync();
    }

    /// <summary>Test-only <see cref="User" /> factory. Each setter
    /// returns the builder so calls chain; <see cref="Build" /> emits
    /// the user entity with sensible defaults for the untouched
    /// fields (active status, today's PasswordChangedAt, etc.).</summary>
    internal sealed class UserBuilder
    {
        private readonly Guid id = Guid.NewGuid();
        private Guid orgId = Guid.NewGuid();
        private readonly Email email = new("alice@example.com");
        private readonly string displayName = "Alice";
        private string status = "active";
        private PasswordHash? passwordHash = new("placeholder-hash-not-real-bcrypt");
        private int failedLoginCount;
        private DateTimeOffset? lockedUntil;
        private DateTimeOffset? lastLoginAt;
        private readonly DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        private readonly DateTimeOffset updatedAt = DateTimeOffset.UtcNow;
        private DateTimeOffset? passwordChangedAt = DateTimeOffset.UtcNow;

        public UserBuilder WithOrgId(Guid value)
        {
            orgId = value;
            return this;
        }

        public UserBuilder WithStatus(string value)
        {
            status = value;
            return this;
        }

        public UserBuilder WithPasswordHash(PasswordHash? value)
        {
            passwordHash = value;
            return this;
        }

        public UserBuilder WithFailedLoginCount(int value)
        {
            failedLoginCount = value;
            return this;
        }

        public UserBuilder WithLockedUntil(DateTimeOffset? value)
        {
            lockedUntil = value;
            return this;
        }

        public UserBuilder WithPasswordChangedAt(DateTimeOffset? value)
        {
            passwordChangedAt = value;
            return this;
        }

        public UserBuilder WithLastLoginAt(DateTimeOffset? value)
        {
            lastLoginAt = value;
            return this;
        }

        public User Build()
        {
            return new User
            {
                Id = id,
                OrgId = orgId,
                Email = email,
                DisplayName = displayName,
                Status = status,
                PasswordHash = passwordHash,
                FailedLoginCount = failedLoginCount,
                LockedUntil = lockedUntil,
                LastLoginAt = lastLoginAt,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                PasswordChangedAt = passwordChangedAt,
            };
        }
    }
}
