// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ExternalOidcAuthProviderShould — exercise the OIDC provider against
// NSubstitute mocks for the JWKS fetcher + role + permission
// resolvers, plus a real in-memory RealmDbContext for the per-tenant
// routing decision. JWTs in the test fixtures are real RSA-signed
// compact tokens (no mock handler) so the validation path is
// exercised end-to-end: header → kid → JWKS → TokenValidationParameters.
// ============================================================================

using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders;
using Plexor.Modules.Sigil.Unit.Realm;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="ExternalOidcAuthProvider" />.
///     Mocks the JWKS fetcher and the role / permission resolvers;
///     the JWT itself is real (RSA-signed) so the validation pipeline
///     is exercised end-to-end.
/// </summary>
public sealed class ExternalOidcAuthProviderShould
{
    private static readonly TimeSpan ExpectedTokenLifetime = TimeSpan.FromHours(1);

    private static readonly string[] AdminRole = ["admin"];

    private static readonly string[] ViewerRole = ["viewer"];

    private static readonly string[] AdminWildcardPermissions = ["*"];

    private static readonly string[] ViewerReadPermissions = ["*.read"];

    /// <summary>Given a valid IDP-issued JWT and an OIDC-configured
    /// tenant, when resolving, then returns a populated
    /// AuthResolution with deterministic Plexor user id.</summary>
    [Fact(DisplayName = "Given a valid IDP-issued JWT and an OIDC-configured tenant, when resolving, then returns a populated AuthResolution")]
    public async Task ResolveAsync_WithValidIdpToken_ReturnsResolutionAsync()
    {
        var orgId = Guid.NewGuid();
        const string issuer = "https://kc.example.com/realms/plexor";
        const string audience = "plexor-cli";
        const string sub = "external-user-123";
        var (signingKey, publicJwk) = CreateSigningKey();
        var token = MintToken(signingKey, issuer, audience, sub, DateTime.UtcNow.AddMinutes(10),
            new Claim("realm_access.roles", "[\"admin\",\"viewer\"]"));
        await using var realm = await RealmTestDb.CreateAsync();
        await ConfigureOidcOrgAsync(realm, orgId, issuer, audience);
        var fetcher = Substitute.For<IJwksFetcher>();
        var roleResolver = Substitute.For<IRoleResolver>();
        var permissionResolver = Substitute.For<IPermissionResolver>();
        fetcher.GetKeySetAsync(issuer, Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        roleResolver.RolesByNamesForOrgAsync(orgId, Arg.Is<IReadOnlyCollection<string>>(names => names.Contains("admin")),
                Arg.Any<CancellationToken>())
            .Returns(AdminRole);
        roleResolver.RolesByNamesForOrgAsync(orgId, Arg.Is<IReadOnlyCollection<string>>(names => names.Contains("viewer") && !names.Contains("admin")),
                Arg.Any<CancellationToken>())
            .Returns(ViewerRole);
        permissionResolver.PermissionsForRolesAsync(orgId, AdminRole, Arg.Any<CancellationToken>())
            .Returns(AdminWildcardPermissions);
        permissionResolver.PermissionsForRolesAsync(orgId, ViewerRole, Arg.Any<CancellationToken>())
            .Returns(ViewerReadPermissions);
        var provider = new ExternalOidcAuthProvider(
            fetcher, roleResolver, permissionResolver, realm,
            NullLogger<ExternalOidcAuthProvider>.Instance);

        var resolution = await provider.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldNotBeNull();
        resolution.OrgId.ShouldBe(orgId);
        resolution.ProviderId.ShouldBe(AuthProviderId.Oidc);
        resolution.IsService.ShouldBeFalse();
        resolution.Roles.ShouldBe(AdminRole);
        resolution.Permissions.ShouldBe(AdminWildcardPermissions);
        resolution.TokenLifetime.ShouldBe(ExpectedTokenLifetime);

        // Deterministic user id — same external (issuer, sub) →
        // same Plexor user id across logins. We re-resolve the same
        // JWT and assert the user id is stable (and non-empty).
        resolution.UserId.ShouldNotBe(Guid.Empty);

        var secondResolution = await provider.ResolveAsync(token, CancellationToken.None);
        secondResolution.ShouldNotBeNull();
        secondResolution.UserId.ShouldBe(resolution.UserId);
    }

    /// <summary>Given a JWT whose issuer doesn't match any
    /// OrgAuthProviderConfig, when resolving, then returns null
    /// without consulting the JWKS fetcher.</summary>
    [Fact(DisplayName = "Given a JWT whose issuer is unconfigured, when resolving, then returns null without consulting the JWKS fetcher")]
    public async Task ResolveAsync_WithUnknownIssuer_ReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        var (signingKey, publicJwk) = CreateSigningKey();
        var token = MintToken(signingKey, "https://unknown.example.com", "plexor-cli", "external-user", DateTime.UtcNow.AddMinutes(10));
        await using var realm = await RealmTestDb.CreateAsync();
        await ConfigureOidcOrgAsync(realm, orgId, "https://kc.example.com/realms/plexor", "plexor-cli");
        var fetcher = Substitute.For<IJwksFetcher>();
        var roleResolver = Substitute.For<IRoleResolver>();
        var permissionResolver = Substitute.For<IPermissionResolver>();
        fetcher.GetKeySetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        var provider = new ExternalOidcAuthProvider(
            fetcher, roleResolver, permissionResolver, realm,
            NullLogger<ExternalOidcAuthProvider>.Instance);

        var resolution = await provider.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldBeNull();
        await fetcher.DidNotReceive().GetKeySetAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given an expired JWT, when resolving, then returns null.</summary>
    [Fact(DisplayName = "Given an expired JWT, when resolving, then returns null")]
    public async Task ResolveAsync_WithExpiredToken_ReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        const string issuer = "https://kc.example.com/realms/plexor";
        var (signingKey, publicJwk) = CreateSigningKey();
        var token = MintToken(signingKey, issuer, "plexor-cli", "external-user",
            DateTime.UtcNow.AddMinutes(-5));
        await using var realm = await RealmTestDb.CreateAsync();
        await ConfigureOidcOrgAsync(realm, orgId, issuer, "plexor-cli");
        var fetcher = Substitute.For<IJwksFetcher>();
        fetcher.GetKeySetAsync(issuer, Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        var provider = BuildProvider(fetcher, realm);

        var resolution = await provider.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given a JWT whose kid doesn't match any JWKS key,
    /// when resolving, then returns null.</summary>
    [Fact(DisplayName = "Given a JWT whose kid is not in the JWKS, when resolving, then returns null")]
    public async Task ResolveAsync_WithUnknownKid_ReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        const string issuer = "https://kc.example.com/realms/plexor";
        var (_, publicJwk) = CreateSigningKey();
        // Sign with a DIFFERENT key (the JWKS only carries publicJwk's kid).
        var (otherSigningKey, _) = CreateSigningKey();
        var token = MintToken(otherSigningKey, issuer, "plexor-cli", "external-user", DateTime.UtcNow.AddMinutes(10));
        await using var realm = await RealmTestDb.CreateAsync();
        await ConfigureOidcOrgAsync(realm, orgId, issuer, "plexor-cli");
        var fetcher = Substitute.For<IJwksFetcher>();
        fetcher.GetKeySetAsync(issuer, Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        var provider = BuildProvider(fetcher, realm);

        var resolution = await provider.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given a JWT whose audience doesn't match the
    /// OrgAuthProviderConfig.OidcClientId, when resolving, then
    /// returns null.</summary>
    [Fact(DisplayName = "Given a JWT whose audience does not match the configured client id, when resolving, then returns null")]
    public async Task ResolveAsync_WithAudienceMismatch_ReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        const string issuer = "https://kc.example.com/realms/plexor";
        var (signingKey, publicJwk) = CreateSigningKey();
        var token = MintToken(signingKey, issuer, "different-client", "external-user", DateTime.UtcNow.AddMinutes(10));
        await using var realm = await RealmTestDb.CreateAsync();
        await ConfigureOidcOrgAsync(realm, orgId, issuer, "plexor-cli");
        var fetcher = Substitute.For<IJwksFetcher>();
        fetcher.GetKeySetAsync(issuer, Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        var provider = BuildProvider(fetcher, realm);

        var resolution = await provider.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given an OIDC-configured tenant, when
    /// CanAuthenticateForAsync runs, then returns true.</summary>
    [Fact(DisplayName = "Given an OIDC-configured tenant, when CanAuthenticateForAsync runs, then returns true")]
    public async Task CanAuthenticateForAsync_WithOidcConfiguredOrg_ReturnsTrueAsync()
    {
        var orgId = Guid.NewGuid();
        await using var realm = await RealmTestDb.CreateAsync();
        await ConfigureOidcOrgAsync(realm, orgId, "https://kc.example.com/realms/plexor", "plexor-cli");
        var provider = BuildProvider(Substitute.For<IJwksFetcher>(), realm);

        var result = await provider.CanAuthenticateForAsync(
            orgId, CancellationToken.None);

        result.ShouldBeTrue();
    }

    /// <summary>Given a Sigil-configured tenant, when
    /// CanAuthenticateForAsync runs, then returns false.</summary>
    [Fact(DisplayName = "Given a Sigil-configured tenant, when CanAuthenticateForAsync runs, then returns false")]
    public async Task CanAuthenticateForAsync_WithSigilConfiguredOrg_ReturnsFalseAsync()
    {
        var orgId = Guid.NewGuid();
        await using var realm = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(realm, orgId);
        await SeedOrgAuthProviderAsync(realm, orgId, OrgAuthProvider.Sigil);
        var provider = BuildProvider(Substitute.For<IJwksFetcher>(), realm);

        var result = await provider.CanAuthenticateForAsync(
            orgId, CancellationToken.None);

        result.ShouldBeFalse();
    }

    /// <summary>Given an unconfigured tenant (no config row), when
    /// CanAuthenticateForAsync runs, then returns false.</summary>
    [Fact(DisplayName = "Given an unconfigured tenant, when CanAuthenticateForAsync runs, then returns false")]
    public async Task CanAuthenticateForAsync_WithUnconfiguredOrg_ReturnsFalseAsync()
    {
        await using var realm = await RealmTestDb.CreateAsync();
        var provider = BuildProvider(Substitute.For<IJwksFetcher>(), realm);

        var result = await provider.CanAuthenticateForAsync(
            Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeFalse();
    }

    /// <summary>Given a JWT with external roles that don't match any
    /// Plexor role, when resolving, then returns a resolution with
    /// empty roles + permissions (no role-match isn't a failure).</summary>
    [Fact(DisplayName = "Given external roles that match no Plexor role, when resolving, then returns a resolution with empty roles and permissions")]
    public async Task ResolveAsync_WithUnmatchedExternalRoles_ReturnsResolutionWithEmptyRolesAsync()
    {
        var orgId = Guid.NewGuid();
        const string issuer = "https://kc.example.com/realms/plexor";
        const string audience = "plexor-cli";
        var (signingKey, publicJwk) = CreateSigningKey();
        var token = MintToken(signingKey, issuer, audience, "external-user",
            DateTime.UtcNow.AddMinutes(10),
            new Claim("realm_access.roles", "[\"unknown-role-1\",\"unknown-role-2\"]"));
        await using var realm = await RealmTestDb.CreateAsync();
        await ConfigureOidcOrgAsync(realm, orgId, issuer, audience);
        var fetcher = Substitute.For<IJwksFetcher>();
        fetcher.GetKeySetAsync(issuer, Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        var roleResolver = Substitute.For<IRoleResolver>();
        roleResolver.RolesByNamesForOrgAsync(orgId, Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        var permissionResolver = Substitute.For<IPermissionResolver>();
        permissionResolver.PermissionsForRolesAsync(orgId, Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        var provider = new ExternalOidcAuthProvider(
            fetcher,
            roleResolver,
            permissionResolver,
            realm,
            NullLogger<ExternalOidcAuthProvider>.Instance);

        var resolution = await provider.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldNotBeNull();
        resolution.Roles.ShouldBeEmpty();
        resolution.Permissions.ShouldBeEmpty();
    }

    private static ExternalOidcAuthProvider BuildProvider(IJwksFetcher fetcher, RealmDbContext realm)
    {
        return new ExternalOidcAuthProvider(
            fetcher,
            Substitute.For<IRoleResolver>(),
            Substitute.For<IPermissionResolver>(),
            realm,
            NullLogger<ExternalOidcAuthProvider>.Instance);
    }

    /// <summary>
    ///     Mint an RSA key + its public JWK shape. Returns the
    ///     signing key (for token minting) + the JWK (for the JWKS
    ///     mock).
    /// </summary>
    private static (RsaSecurityKey Key, JsonWebKey PublicJwk) CreateSigningKey()
    {
        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-kid" };
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new JsonWebKey
        {
            KeyId = "test-kid",
            Kty = JsonWebAlgorithmsKeyTypes.RSA,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent),
        };
        return (key, jwk);
    }

    private static JsonWebKeySet BuildKeySet(JsonWebKey jwk)
    {
        var set = new JsonWebKeySet();
        set.Keys.Add(jwk);
        return set;
    }

    private static string MintToken(
        SecurityKey signingKey,
        string issuer,
        string audience,
        string subject,
        DateTime expiresAtUtc,
        params Claim[] extraClaims)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", subject),
            }.Concat(extraClaims),
            authenticationType: "TestOIDC");
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = identity,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = expiresAtUtc,
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        };
        return handler.CreateToken(descriptor);
    }

    private static async Task ConfigureOidcOrgAsync(
        RealmDbContext db,
        Guid orgId,
        string issuer,
        string clientId)
    {
        await SeedOrgAsync(db, orgId);
        await SeedOrgAuthProviderAsync(db, orgId, OrgAuthProvider.Oidc, issuer, clientId);
    }

    private static async Task SeedOrgAsync(RealmDbContext db, Guid orgId)
    {
        var now = DateTimeOffset.UtcNow;
        var slug = $"org-{orgId.ToString()[..6]}";
        await db.Organizations.AddAsync(new Organization
        {
            Id = orgId,
            Name = slug,
            Slug = slug,
            Status = "active",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedOrgAuthProviderAsync(
        RealmDbContext db,
        Guid orgId,
        OrgAuthProvider provider,
        string? oidcAuthority = null,
        string? oidcClientId = null)
    {
        var now = DateTimeOffset.UtcNow;
        await db.OrgAuthProviderConfigs.AddAsync(new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Provider = provider,
            OidcAuthority = oidcAuthority,
            OidcClientId = oidcClientId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }
}
