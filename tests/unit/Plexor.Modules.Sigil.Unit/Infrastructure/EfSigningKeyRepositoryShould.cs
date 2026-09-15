// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfSigningKeyRepositoryShould — exercises the signing-key repository
// against an in-memory IdentityDbContext. The repository is the
// canonical read path for JWT verification: GetActiveAsync returns
// the key the signer uses; GetByKidAsync returns a specific kid;
// ListActiveAsync returns the rotation window.
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="EfSigningKeyRepository" />.
///     Each test seeds one or more <see cref="SigningKey" /> rows with
///     distinct kid + NotAfter combinations, then asserts the
///     repository returns the expected subset.
/// </summary>
public sealed class EfSigningKeyRepositoryShould
{
    /// <summary>Verifies that <see cref="EfSigningKeyRepository.GetActiveAsync" />
    /// returns the most-recent key whose <c>NotAfter</c> is still
    /// <c>null</c> — the one the JWT signer uses.</summary>
    [Fact(DisplayName = "Given two active keys with distinct kids, when GetActiveAsync, then returns the most recent one")]
    public async Task GetActiveAsyncReturnsMostRecentActiveAsync()
    {
        await using var db = await TestDb.CreateAsync();
        await SeedSigningKeyAsync(db, kid: "key_old", createdAt: DateTimeOffset.UtcNow.AddDays(-30), notAfter: null);
        await SeedSigningKeyAsync(db, kid: "key_new", createdAt: DateTimeOffset.UtcNow, notAfter: null);

        var repository = new EfSigningKeyRepository(db, TimeProvider.System);
        var active = await repository.GetActiveAsync();

        active.ShouldNotBeNull();
        active.Kid.ShouldBe("key_new");
    }

    /// <summary>Verifies that <see cref="EfSigningKeyRepository.GetActiveAsync" />
    /// returns <c>null</c> when the table is empty — the bootstrapper
    /// contract is "if no active key, generate one".</summary>
    [Fact(DisplayName = "Given no keys, when GetActiveAsync, then returns null")]
    public async Task GetActiveAsyncReturnsNullWhenEmptyAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var repository = new EfSigningKeyRepository(db, TimeProvider.System);

        var active = await repository.GetActiveAsync();

        active.ShouldBeNull();
    }

    /// <summary>Verifies that <see cref="EfSigningKeyRepository.ListActiveAsync" />
    /// returns every key whose <c>NotAfter</c> is null or in the
    /// future, sorted newest-first. The window covers rotation: the
    /// verifier still accepts tokens signed with a recently rotated
    /// key during the access-JWT lifetime.</summary>
    [Fact(DisplayName = "Given rotated + active keys, when ListActiveAsync, then returns active window newest-first")]
    public async Task ListActiveAsyncReturnsActiveWindowAsync()
    {
        await using var db = await TestDb.CreateAsync();
        await SeedSigningKeyAsync(
            db, kid: "key_stale", createdAt: DateTimeOffset.UtcNow.AddDays(-90),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        await SeedSigningKeyAsync(
            db, kid: "key_recently_rotated",
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            notAfter: DateTimeOffset.UtcNow.AddHours(1));
        await SeedSigningKeyAsync(
            db, kid: "key_current", createdAt: DateTimeOffset.UtcNow,
            notAfter: null);

        var repository = new EfSigningKeyRepository(db, TimeProvider.System);
        var active = await repository.ListActiveAsync();

        active.Select(static key => key.Kid).ShouldBe(
            ["key_current", "key_recently_rotated"]);
    }

    private static async Task SeedSigningKeyAsync(
        IdentityDbContext db,
        string kid,
        DateTimeOffset createdAt,
        DateTimeOffset? notAfter)
    {
        await db.SigningKeys.AddAsync(new SigningKey
        {
            Kid = kid,
            Algorithm = "ES256",
            PublicKeyPem = $"-----BEGIN PUBLIC KEY-----\n{kid}\n-----END PUBLIC KEY-----",
            PrivateKeyPem = $"-----BEGIN PRIVATE KEY-----\n{kid}\n-----END PRIVATE KEY-----",
            CreatedAt = createdAt,
            NotAfter = notAfter,
        });
        await db.SaveChangesAsync();
    }
}
