// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfRefreshTokenStoreShould — exercises the refresh-token store
// against an in-memory IdentityDbContext. The store is responsible
// for token-issue hashing + atomic rotation + replay detection; these
// tests verify the contract behaviour without a real Postgres.
// ============================================================================

using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="EfRefreshTokenStore" />.
///     Uses an in-memory <c>IdentityDbContext</c> to exercise the
///     store's CRUD + rotation contract without spinning up
///     Postgres. Each test starts with a fresh database.
/// </summary>
public sealed class EfRefreshTokenStoreShould
{
    /// <summary>Verifies that <see cref="EfRefreshTokenStore.IssueAsync" />
    /// persists a new token with a fresh id + family id and the
    /// SHA-256 hash of the raw token (not the raw token).</summary>
    [Fact(DisplayName = "Given a raw token, when IssueAsync, then persists with new id, fresh family id, and SHA-256 hash")]
    public async Task IssueAsyncStoresHashWithFreshFamilyAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var store = new EfRefreshTokenStore(db);
        var userId = Guid.NewGuid();
        const string rawToken = "raw-token-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var token = await store.IssueAsync(userId, rawToken, expiresAt);

        token.Id.ShouldNotBe(Guid.Empty);
        token.UserId.ShouldBe(userId);
        token.FamilyId.ShouldNotBe(Guid.Empty);
        token.ExpiresAt.ShouldBe(expiresAt);
        token.RevokedAt.ShouldBeNull();
        token.ReplacedBy.ShouldBeNull();
        token.TokenHash.ShouldBe(RefreshTokenHasher.Hash(rawToken));
        token.TokenHash.ShouldNotBe(rawToken);
    }

    /// <summary>Verifies that <see cref="EfRefreshTokenStore.FindByRawTokenAsync" />
    /// returns the persisted token by its hash, and <c>null</c> for
    /// an unknown token.</summary>
    [Fact(DisplayName = "Given an unknown raw token, when FindByRawTokenAsync, then returns null")]
    public async Task FindByRawTokenAsyncReturnsNullForUnknownAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var store = new EfRefreshTokenStore(db);
        var userId = Guid.NewGuid();
        await store.IssueAsync(userId, "raw-token-aaa", DateTimeOffset.UtcNow.AddDays(1));

        var match = await store.FindByRawTokenAsync("raw-token-aaa");
        var miss = await store.FindByRawTokenAsync("never-issued-token");

        match.ShouldNotBeNull();
        match.UserId.ShouldBe(userId);
        miss.ShouldBeNull();
    }

    /// <summary>Verifies that <see cref="EfRefreshTokenStore.RotateAsync" />
    /// atomically advances the chain — old token is marked revoked
    /// with <c>ReplacedBy</c> pointing at the new id, new token lives
    /// in the same family, and the rotation reports
    /// <see cref="RefreshRotationResult.Success" />.</summary>
    [Fact(DisplayName = "Given an active token, when RotateAsync, then old is revoked and new persists in same family")]
    public async Task RotateAsyncAdvancesChainAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var store = new EfRefreshTokenStore(db);
        var userId = Guid.NewGuid();
        const string oldRaw = "old-raw-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string newRaw = "new-raw-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var first = await store.IssueAsync(userId, oldRaw, DateTimeOffset.UtcNow.AddDays(30));

        var result = await store.RotateAsync(oldRaw, newRaw, DateTimeOffset.UtcNow.AddDays(30));

        result.ShouldBe(RefreshRotationResult.Success);
        var second = await store.FindByRawTokenAsync(newRaw);
        second.ShouldNotBeNull();
        second.FamilyId.ShouldBe(first.FamilyId);
        second.UserId.ShouldBe(userId);
        second.RevokedAt.ShouldBeNull();

        var nowRevoked = await store.FindByRawTokenAsync(oldRaw);
        nowRevoked.ShouldNotBeNull();
        nowRevoked.RevokedAt.ShouldNotBeNull();
        nowRevoked.ReplacedBy.ShouldBe(second.Id);
    }

    /// <summary>Verifies that <see cref="EfRefreshTokenStore.RotateAsync" />
    /// surfaces a previously-revoked token as
    /// <see cref="RefreshRotationResult.Replayed" /> without writing
    /// a new token — the auth service uses this to trigger family
    /// revocation.</summary>
    [Fact(DisplayName = "Given an already-revoked token, when RotateAsync, then returns Replayed and writes nothing")]
    public async Task RotateAsyncReturnsReplayedForAlreadyRevokedAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var store = new EfRefreshTokenStore(db);
        var userId = Guid.NewGuid();
        const string raw = "replay-raw-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        await store.IssueAsync(userId, raw, DateTimeOffset.UtcNow.AddDays(30));
        await store.RotateAsync(raw, "next-raw-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow.AddDays(30));

        var second = await store.RotateAsync(raw, "never-issued-raw-aaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset.UtcNow.AddDays(30));

        second.ShouldBe(RefreshRotationResult.Replayed);
        var ghost = await store.FindByRawTokenAsync("never-issued-raw-aaaaaaaaaaaaaaaaaaaaaa");
        ghost.ShouldBeNull();
    }

    /// <summary>Verifies that <see cref="EfRefreshTokenStore.RotateAsync" />
    /// returns <see cref="RefreshRotationResult.NotFound" /> when the
    /// token has never been seen — distinct from
    /// <see cref="RefreshRotationResult.Replayed" />, which fires on
    /// a known-but-revoked token.</summary>
    [Fact(DisplayName = "Given an unknown token, when RotateAsync, then returns NotFound")]
    public async Task RotateAsyncReturnsNotFoundForUnknownAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var store = new EfRefreshTokenStore(db);

        var result = await store.RotateAsync(
            "never-seen-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "next-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            DateTimeOffset.UtcNow.AddDays(30));

        result.ShouldBe(RefreshRotationResult.NotFound);
    }
}
