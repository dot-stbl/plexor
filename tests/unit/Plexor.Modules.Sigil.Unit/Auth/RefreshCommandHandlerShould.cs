// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshCommandHandlerShould — behavioural coverage of the
// security-critical refresh-token rotation path. The store +
// issuer + owner-resolver are mocked (NSubstitute) so each test
// exercises exactly one failure mode or the happy path. No real
// DbContext is needed because the handler's owner + role lookups
// go through IRefreshTokenOwnerResolver (extracted for testability
// — see IRefreshTokenOwnerResolver.cs).
// ============================================================================

using NSubstitute;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Auth;

/// <summary>
///     Behavioural tests for <see cref="RefreshCommandHandler" />.
///     Exercises the security-critical rotation path (rotation,
///     replay detection, family revocation, expiration, deleted-user
///     guard) with the store + issuer + owner-resolver mocked via
///     NSubstitute. No real database is required.
/// </summary>
public sealed class RefreshCommandHandlerShould
{
    /// <summary>
    ///     Happy path: the store rotates successfully, the owner
    ///     resolver returns a real user, the issuer hands back an
    ///     access JWT, and the result carries a fresh refresh
    ///     token distinct from the one the client presented.
    /// </summary>
    [Fact(DisplayName = "Given valid refresh token, when Refresh, then returns new access + refresh pair")]
    public async Task RefreshWithValidTokenReturnsNewPairAsync()
    {
        var user = NewUser();
        var store = Substitute.For<IRefreshTokenStore>();
        var issuer = Substitute.For<ITokenIssuer>();
        var ownerResolver = Substitute.For<IRefreshTokenOwnerResolver>();
        WireSuccessfulRotation(store, issuer, ownerResolver, user);

        var sut = new RefreshCommandHandler(store, issuer, ownerResolver, TimeProvider.System);

        var result = await sut.HandleAsync(new RefreshCommand("presented-refresh-token"));

        result.AccessToken.ShouldBe("access-jwt-xyz");
        result.RefreshToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBe("presented-refresh-token");
        result.AccessTokenExpiresAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        await issuer.Received(1).IssueAsync(user.Id, user.OrgId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     The presented token's hash is not in the DB — either
    ///     never issued, or hard-deleted after a family revocation.
    ///     The handler MUST throw InvalidCredentials without
    ///     touching the owner resolver (no FindByTokenHashAsync,
    ///     no user join).
    /// </summary>
    [Fact(DisplayName = "Given unknown refresh token, when Refresh, then throws InvalidCredentials and does not call revocation")]
    public async Task RefreshWithUnknownTokenThrowsInvalidCredentialsAsync()
    {
        var store = Substitute.For<IRefreshTokenStore>();
        store.RotateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.NotFound);
        var issuer = Substitute.For<ITokenIssuer>();
        var ownerResolver = Substitute.For<IRefreshTokenOwnerResolver>();

        var sut = new RefreshCommandHandler(store, issuer, ownerResolver, TimeProvider.System);

        var ex = await Should.ThrowAsync<IdentityException>(
            () => sut.HandleAsync(new RefreshCommand("never-issued-token")));

        ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);
        await store.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().FindByRawTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await ownerResolver.DidNotReceive().ResolveByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     The presented token has already been rotated (or revoked).
    ///     The store reports Replayed; the handler MUST look up the
    ///     token's family and revoke every other token in it before
    ///     throwing RefreshTokenReplayed. This is the security
    ///     boundary: a leaked refresh token cannot be reused without
    ///     invalidating the entire rotation family.
    /// </summary>
    [Fact(DisplayName = "Given replayed refresh token, when Refresh, then throws RefreshTokenReplayed and revokes the family")]
    public async Task RefreshWithReplayedTokenThrowsAndRevokesFamilyAsync()
    {
        var familyId = Guid.NewGuid();
        var replayedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FamilyId = familyId,
            TokenHash = "hash-of-replayed-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var store = Substitute.For<IRefreshTokenStore>();
        store.RotateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.Replayed);
        store.FindByRawTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(replayedToken);
        var issuer = Substitute.For<ITokenIssuer>();
        var ownerResolver = Substitute.For<IRefreshTokenOwnerResolver>();

        var sut = new RefreshCommandHandler(store, issuer, ownerResolver, TimeProvider.System);

        var ex = await Should.ThrowAsync<IdentityException>(
            () => sut.HandleAsync(new RefreshCommand("replayed-token")));

        ex.Code.ShouldBe(IdentityExceptions.RefreshTokenReplayed);
        await store.Received(1).RevokeFamilyAsync(familyId, Arg.Any<CancellationToken>());
        await issuer.DidNotReceive().IssueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await ownerResolver.DidNotReceive().ResolveByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     After a family revocation, the previously-presented token
    ///     is no longer in the DB (revoked-then-cleaned, or simply
    ///     the row was rotated off the chain). Rotating returns
    ///     NotFound — the handler surfaces InvalidCredentials with no
    ///     leakage about whether the token ever existed.
    /// </summary>
    [Fact(DisplayName = "Given refresh token no longer present after family revocation, when Refresh, then throws InvalidCredentials")]
    public async Task RefreshAfterFamilyRevokedThrowsInvalidCredentialsAsync()
    {
        var store = Substitute.For<IRefreshTokenStore>();
        store.RotateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.NotFound);
        var issuer = Substitute.For<ITokenIssuer>();
        var ownerResolver = Substitute.For<IRefreshTokenOwnerResolver>();

        var sut = new RefreshCommandHandler(store, issuer, ownerResolver, TimeProvider.System);

        var ex = await Should.ThrowAsync<IdentityException>(
            () => sut.HandleAsync(new RefreshCommand("orphan-token-from-revoked-family")));

        ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);
        await store.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     The presented token's expires_at is in the past. The store
    ///     returns Expired; the handler MUST throw
    ///     RefreshTokenExpired — distinct from the Replayed path so
    ///     the FE can prompt re-login vs. force logout. The handler
    ///     MUST NOT revoke the family: an expired token is normal
    ///     lifecycle, not a compromise signal.
    /// </summary>
    [Fact(DisplayName = "Given expired refresh token, when Refresh, then throws RefreshTokenExpired and does not revoke the family")]
    public async Task RefreshWithExpiredTokenThrowsTokenExpiredAsync()
    {
        var store = Substitute.For<IRefreshTokenStore>();
        store.RotateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.Expired);
        var issuer = Substitute.For<ITokenIssuer>();
        var ownerResolver = Substitute.For<IRefreshTokenOwnerResolver>();

        var sut = new RefreshCommandHandler(store, issuer, ownerResolver, TimeProvider.System);

        var ex = await Should.ThrowAsync<IdentityException>(
            () => sut.HandleAsync(new RefreshCommand("expired-token")));

        ex.Code.ShouldBe(IdentityExceptions.RefreshTokenExpired);
        await store.DidNotReceive().RevokeFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().FindByRawTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await ownerResolver.DidNotReceive().ResolveByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     The store rotated successfully, but the token's owner has
    ///     been deleted from sigil.users since the refresh token was
    ///     issued. The owner resolver returns null — the handler
    ///     MUST surface InvalidCredentials (generic 401) rather than
    ///     letting an InvalidOperationException leak up as a 500.
    ///     Same generic error as every other auth failure — no
    ///     information leakage about user existence.
    /// </summary>
    [Fact(DisplayName = "Given refresh token whose owner was deleted, when Refresh, then throws InvalidCredentials")]
    public async Task RefreshForDeletedUserThrowsInvalidCredentialsAsync()
    {
        var store = Substitute.For<IRefreshTokenStore>();
        store.RotateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.Success);
        var issuer = Substitute.For<ITokenIssuer>();
        var ownerResolver = Substitute.For<IRefreshTokenOwnerResolver>();
        ownerResolver.ResolveByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var sut = new RefreshCommandHandler(store, issuer, ownerResolver, TimeProvider.System);

        var ex = await Should.ThrowAsync<IdentityException>(
            () => sut.HandleAsync(new RefreshCommand("orphan-refresh-token")));

        ex.Code.ShouldBe(IdentityExceptions.InvalidCredentials);
        await issuer.DidNotReceive().IssueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Wire a successful rotation: store.RotateAsync returns
    ///     Success, the owner resolver returns the supplied user,
    ///     and the issuer hands back a fake JWT. Used by the happy
    ///     path test.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="issuer"></param>
    /// <param name="ownerResolver"></param>
    /// <param name="user"></param>
    private static void WireSuccessfulRotation(
        IRefreshTokenStore store,
        ITokenIssuer issuer,
        IRefreshTokenOwnerResolver ownerResolver,
        User user)
    {
        store.RotateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.Success);

        ownerResolver.ResolveByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(user);
        ownerResolver.LoadRoleNamesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        issuer.IssueAsync(user.Id, user.OrgId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedAccessToken(
                CompactJwt: "access-jwt-xyz",
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15)));
    }

    private static User NewUser()
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            Email = new Email("user@example.com"),
            DisplayName = "Test User",
            Status = "active",
            FailedLoginCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
            PasswordChangedAt = now,
        };
    }
}
