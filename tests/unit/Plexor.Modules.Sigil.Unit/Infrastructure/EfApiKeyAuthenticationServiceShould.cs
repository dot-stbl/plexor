// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfApiKeyAuthenticationServiceShould — exercises the API-key auth
// service against an in-memory IdentityDbContext. The service does
// kid → DB lookup → SHA-256 constant-time compare → principal build;
// these tests verify each branch without a real Postgres.
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="EfApiKeyAuthenticationService" />.
///     Each test seeds an <c>IdentityDbContext</c> with one or more
///     API keys (and the SHA-256 hashes of their secrets) and walks
///     the happy + sad paths through <see cref="EfApiKeyAuthenticationService.AuthenticateAsync" />.
/// </summary>
public sealed class EfApiKeyAuthenticationServiceShould
{
    /// <summary>Verifies that a key with a matching secret returns
    /// <see cref="ApiKeyAuthenticationResult.Success" /> whose principal
    /// carries the stored <c>permission</c> claims plus the
    /// <c>is_service</c> flag.</summary>
    [Fact(DisplayName = "Given matching kid + secret, when AuthenticateAsync, then returns Success with permission + is_service claims")]
    public async Task MatchingSecretReturnsSuccessAsync()
    {
        await using var db = await TestDb.CreateAsync();
        const string rawSecret = "raw-secret-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var key = await SeedApiKeyAsync(
            db,
            rawSecret,
            [new PermissionScope("compute.vms.read"), new PermissionScope("iam.users.read")]);

        var service = new EfApiKeyAuthenticationService(db);
        var result = await service.AuthenticateAsync(key.Id, rawSecret);

        var success = result.ShouldBeOfType<ApiKeyAuthenticationResult.Success>();
        success.KeyId.ShouldBe(key.Id);
        var permissions = success.Principal
            .FindAll(IdentityClaims.Permission)
            .Select(static claim => claim.Value)
            .ToArray();
        permissions.ShouldBe(["compute.vms.read", "iam.users.read"]);
        success.Principal.FindFirst(IdentityClaims.IsService)?.Value.ShouldBe("true");
        success.Principal.FindFirst(IdentityClaims.UserId)?.Value.ShouldBe(key.UserId.ToString());
        success.Principal.FindFirst(IdentityClaims.TenantId)?.Value.ShouldBe(key.OrgId.ToString());
    }

    /// <summary>Verifies that an unknown kid yields
    /// <see cref="ApiKeyAuthenticationResult.NotFound" /> — distinct
    /// from <see cref="ApiKeyAuthenticationResult.Invalid" /> so the
    /// bearer handler can surface a different reason if it ever
    /// wants to.</summary>
    [Fact(DisplayName = "Given unknown kid, when AuthenticateAsync, then returns NotFound")]
    public async Task UnknownKidReturnsNotFoundAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var service = new EfApiKeyAuthenticationService(db);

        var result = await service.AuthenticateAsync(Guid.NewGuid(), "anything");

        result.ShouldBeOfType<ApiKeyAuthenticationResult.NotFound>();
    }

    /// <summary>Verifies that the right kid but the wrong secret
    /// yields <see cref="ApiKeyAuthenticationResult.Invalid" />
    /// with a "secret mismatch" reason. The constant-time
    /// <c>FixedTimeEquals</c> path is exercised but its
    /// timing-resistance property is not asserted here.</summary>
    [Fact(DisplayName = "Given right kid + wrong secret, when AuthenticateAsync, then returns Invalid with mismatch reason")]
    public async Task WrongSecretReturnsInvalidAsync()
    {
        await using var db = await TestDb.CreateAsync();
        var key = await SeedApiKeyAsync(
            db,
            "correct-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            [new PermissionScope("compute.vms.read")]);

        var service = new EfApiKeyAuthenticationService(db);
        var result = await service.AuthenticateAsync(key.Id, "totally-wrong-secret-bbbbbbbbbbbb");

        var invalid = result.ShouldBeOfType<ApiKeyAuthenticationResult.Invalid>();
        invalid.Reason.ShouldBe("API key secret mismatch.");
    }

    /// <summary>Verifies that a revoked key is rejected with
    /// <see cref="ApiKeyAuthenticationResult.Invalid" /> carrying
    /// an "API key revoked" reason, even when the secret would
    /// otherwise match.</summary>
    [Fact(DisplayName = "Given revoked key, when AuthenticateAsync, then returns Invalid with revoked reason")]
    public async Task RevokedKeyReturnsInvalidAsync()
    {
        await using var db = await TestDb.CreateAsync();
        const string rawSecret = "raw-secret-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var key = await SeedApiKeyAsync(
            db,
            rawSecret,
            [new PermissionScope("compute.vms.read")]);
        await db.ApiKeys
            .Where(k => k.Id == key.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(k => k.RevokedAt, DateTimeOffset.UtcNow));

        var service = new EfApiKeyAuthenticationService(db);
        var result = await service.AuthenticateAsync(key.Id, rawSecret);

        var invalid = result.ShouldBeOfType<ApiKeyAuthenticationResult.Invalid>();
        invalid.Reason.ShouldBe("API key revoked.");
    }

    private static async Task<ApiKey> SeedApiKeyAsync(
        IdentityDbContext db,
        string rawSecret,
        IReadOnlyList<PermissionScope> permissions)
    {
        var hashBytes = await SHA256.HashDataAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(rawSecret), writable: false));
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "test-key",
            SecretHash = Convert.ToHexString(hashBytes).ToLowerInvariant(),
            Permissions = permissions,
            ExpiresAt = null,
            RevokedAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await db.ApiKeys.AddAsync(key);
        await db.SaveChangesAsync();
        return key;
    }
}
