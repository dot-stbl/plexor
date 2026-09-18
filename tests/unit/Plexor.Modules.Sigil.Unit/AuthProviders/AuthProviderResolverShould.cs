// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthProviderResolverShould — exercise the dispatch boundary between
// Sigil and external OIDC providers. The resolver peeks the JWT
// `iss` claim, routes to the right IAuthProvider, and caches the
// (iss → provider) map in IMemoryCache for 5 minutes.
//
// Tests use NSubstitute stubs for the Sigil + OIDC providers'
// dependencies (JWT signer, JWKS fetcher, role / permission resolvers)
// and a substitute IOrgAuthProviderConfigReader for the
// OrgAuthProviderConfig lookups. The resolver itself is real — every
// test exercises the production algorithm.
// ============================================================================

using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="AuthProviderResolver" />.
///     The resolver is a thin dispatcher — the value lives in the
///     routing decisions (which provider owns which <c>iss</c>) and
///     the cache hit / miss behaviour.
/// </summary>
public sealed class AuthProviderResolverShould
{
    /// <summary>
    ///     Issuer value the Sigil JWT signer always sets
    ///     (mirrors <see cref="IdentityClaims.IssuerValue" />).
    ///     Resolver matches Sigil-issued tokens by exclusion against
    ///     this string.
    /// </summary>
    private const string SigilIssuerValue = "plexor";

    private const string TestOidcIssuer = "https://kc.example.com/realms/plexor";

    private static readonly TimeSpan ExpectedTokenLifetime = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan ExpectedOidcTokenLifetime = TimeSpan.FromHours(1);

    private static readonly string[] EmptyRoles = [];

    private static readonly string[] EmptyPermissions = [];

    private static readonly string[] AdminRole = ["admin"];

    private static readonly string[] AdminWildcardPermissions = ["*"];

    /// <summary>
    ///     Wire the full resolver + both provider concretes with
    ///     NSubstitute stubs. The default config reader returns
    ///     null — callers configure it when they want the OIDC
    ///     routing to succeed.
    /// </summary>
    private static (AuthProviderResolver Resolver, NSubstituteMocks Mocks) BuildResolver(
        IOrgAuthProviderConfigReader configReader)
    {
        var mocks = new NSubstituteMocks(
            Substitute.For<IJwtSigningService>(),
            Substitute.For<IUserLookup>(),
            Substitute.For<IRoleResolver>(),
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IJwksFetcher>());

        var sigilProvider = new SigilAuthProvider(
            mocks.Signing,
            mocks.UserLookup,
            mocks.RoleResolver,
            mocks.PermissionResolver,
            configReader,
            NullLogger<SigilAuthProvider>.Instance);

        var oidcProvider = new ExternalOidcAuthProvider(
            mocks.JwksFetcher,
            mocks.RoleResolver,
            mocks.PermissionResolver,
            configReader,
            NullLogger<ExternalOidcAuthProvider>.Instance);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new AuthProviderResolver(
            sigilProvider,
            oidcProvider,
            configReader,
            cache,
            NullLogger<AuthProviderResolver>.Instance);
        return (resolver, mocks);
    }

    /// <summary>
    ///     Build a seeded <see cref="OrgAuthProviderConfig" /> row
    ///     for an OIDC-issuer lookup — the substitute reader
    ///     returns it on every <c>GetByOidcIssuerAsync</c> call,
    ///     mirroring the production EF read against
    ///     <c>realm.org_auth_provider_configs</c>.
    /// </summary>
    /// <param name="orgId">Tenant id.</param>
    /// <param name="oidcAuthority">OIDC issuer URL.</param>
    /// <param name="oidcClientId">OIDC client id (audience).</param>
    private static OrgAuthProviderConfig BuildOidcConfig(
        Guid orgId,
        string oidcAuthority,
        string oidcClientId)
    {
        var now = DateTimeOffset.UtcNow;
        return new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Provider = OrgAuthProvider.Oidc,
            OidcAuthority = oidcAuthority,
            OidcClientId = oidcClientId,
            OidcScopes = ["openid", "profile", "email"],
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Given an empty credential, when resolving, then returns
    /// null and never touches any provider.</summary>
    [Fact(DisplayName = "Given an empty credential, when resolving, then returns null without consulting any provider")]
    public async Task ResolveAsync_WithEmptyCredential_ReturnsNullAsync()
    {
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        var (resolver, mocks) = BuildResolver(configReader);

        var resolution = await resolver.ResolveAsync(string.Empty, CancellationToken.None);

        resolution.ShouldBeNull();
        await mocks.Signing.DidNotReceive().VerifyAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await mocks.JwksFetcher.DidNotReceive().GetKeySetAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given a whitespace credential, when resolving, then
    /// returns null (treated as empty).</summary>
    [Fact(DisplayName = "Given a whitespace credential, when resolving, then returns null")]
    public async Task ResolveAsync_WithWhitespaceCredential_ReturnsNullAsync()
    {
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        var (resolver, _) = BuildResolver(configReader);

        var resolution = await resolver.ResolveAsync("   ", CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given a credential that isn't a JWT (no dots), when
    /// resolving, then returns null.</summary>
    [Fact(DisplayName = "Given a non-JWT credential (no dots), when resolving, then returns null")]
    public async Task ResolveAsync_WithMalformedCredential_ReturnsNullAsync()
    {
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        var (resolver, _) = BuildResolver(configReader);

        var resolution = await resolver.ResolveAsync("not-a-jwt", CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given a Sigil-issued JWT (iss=plexor), when resolving,
    /// then dispatches to SigilAuthProvider and returns its result.</summary>
    [Fact(DisplayName = "Given a Sigil-issued JWT, when resolving, then dispatches to SigilAuthProvider")]
    public async Task ResolveAsync_WithSigilIssuer_DispatchesToSigilProviderAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var token = MintUnsignedJwt(SigilIssuerValue, audience: null, subject: null);
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        var (resolver, mocks) = BuildResolver(configReader);
        mocks.Signing.VerifyAsync(token, Arg.Any<CancellationToken>())
            .Returns(BuildSigilSuccess(userId, orgId));
        mocks.UserLookup.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = userId,
                OrgId = orgId,
                Email = new Email("alice@example.com"),
                DisplayName = "Alice",
                Status = "active",
            });
        mocks.RoleResolver.ResolveAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(AdminRole);
        mocks.PermissionResolver.ResolveAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(AdminWildcardPermissions);

        var resolution = await resolver.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldNotBeNull();
        resolution.ProviderId.ShouldBe(AuthProviderId.Sigil);
        resolution.OrgId.ShouldBe(orgId);
        resolution.UserId.ShouldBe(userId);
        resolution.Roles.ShouldBe(AdminRole);
        resolution.Permissions.ShouldBe(AdminWildcardPermissions);
        resolution.TokenLifetime.ShouldBe(ExpectedTokenLifetime);

        await mocks.Signing.Received(1).VerifyAsync(token, Arg.Any<CancellationToken>());
        await mocks.JwksFetcher.DidNotReceive().GetKeySetAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given an OIDC-issued JWT (iss=https://kc.example.com/...)
    /// and a tenant configured for Oidc, when resolving, then
    /// dispatches to ExternalOidcAuthProvider.</summary>
    [Fact(DisplayName = "Given an OIDC-issued JWT and an OIDC-configured tenant, when resolving, then dispatches to ExternalOidcAuthProvider")]
    public async Task ResolveAsync_WithOidcIssuer_DispatchesToOidcProviderAsync()
    {
        var orgId = Guid.NewGuid();
        const string clientId = "plexor-cli";
        var (signingKey, publicJwk) = CreateSigningKey();
        var token = MintRsaSignedJwt(signingKey, TestOidcIssuer, clientId, "external-user-123",
            DateTime.UtcNow.AddMinutes(10));
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        configReader.GetByOidcIssuerAsync(TestOidcIssuer, Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(orgId, TestOidcIssuer, clientId));
        var (resolver, mocks) = BuildResolver(configReader);
        mocks.JwksFetcher.GetKeySetAsync(TestOidcIssuer, Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        mocks.RoleResolver.RolesByNamesForOrgAsync(orgId, Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(AdminRole);
        mocks.PermissionResolver.PermissionsForRolesAsync(orgId, Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(AdminWildcardPermissions);

        var resolution = await resolver.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldNotBeNull();
        resolution.ProviderId.ShouldBe(AuthProviderId.Oidc);
        resolution.OrgId.ShouldBe(orgId);
        resolution.TokenLifetime.ShouldBe(ExpectedOidcTokenLifetime);

        await mocks.JwksFetcher.Received(1).GetKeySetAsync(
            TestOidcIssuer, Arg.Any<CancellationToken>());
        await mocks.Signing.DidNotReceive().VerifyAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given an OIDC-issued JWT whose issuer is not registered
    /// against any OrgAuthProviderConfig, when resolving, then returns
    /// null and never consults the JWKS fetcher.</summary>
    [Fact(DisplayName = "Given an OIDC-issued JWT with an unregistered issuer, when resolving, then returns null without consulting the JWKS fetcher")]
    public async Task ResolveAsync_WithUnknownIssuer_ReturnsNullAsync()
    {
        const string rogueIssuer = "https://unknown-idp.example.com";
        var (signingKey, _) = CreateSigningKey();
        var token = MintRsaSignedJwt(signingKey, rogueIssuer, "plexor-cli", "user",
            DateTime.UtcNow.AddMinutes(10));
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        var (resolver, mocks) = BuildResolver(configReader);

        var resolution = await resolver.ResolveAsync(token, CancellationToken.None);

        resolution.ShouldBeNull();
        await mocks.JwksFetcher.DidNotReceive().GetKeySetAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await mocks.Signing.DidNotReceive().VerifyAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given the same OIDC issuer twice in succession, when
    /// resolving, then the second call serves from cache (no
    /// OrgAuthProviderConfig DB hit + no JWKS fetcher call).</summary>
    /// <remarks>
    ///     <para>Implements the cache test by asserting that the
    ///     substitute config reader is hit exactly once — the second
    ///     call short-circuits on the (iss → provider) cache and
    ///     never reaches the reader.</para>
    /// </remarks>
    [Fact(DisplayName = "Given the same OIDC issuer on a second resolve, when resolving, then serves from cache without re-querying OrgAuthProviderConfig")]
    public async Task ResolveAsync_CachesProviderByIssuer_SecondCallDoesNotQueryOrgAsync()
    {
        var orgId = Guid.NewGuid();
        const string clientId = "plexor-cli";
        var (signingKey, publicJwk) = CreateSigningKey();
        var firstToken = MintRsaSignedJwt(signingKey, TestOidcIssuer, clientId, "user-1",
            DateTime.UtcNow.AddMinutes(10));
        var secondToken = MintRsaSignedJwt(signingKey, TestOidcIssuer, clientId, "user-2",
            DateTime.UtcNow.AddMinutes(10));
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        configReader.GetByOidcIssuerAsync(TestOidcIssuer, Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(orgId, TestOidcIssuer, clientId));
        var (resolver, mocks) = BuildResolver(configReader);
        mocks.JwksFetcher.GetKeySetAsync(TestOidcIssuer, Arg.Any<CancellationToken>())
            .Returns(BuildKeySet(publicJwk));
        mocks.RoleResolver.RolesByNamesForOrgAsync(orgId, Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyRoles);
        mocks.PermissionResolver.PermissionsForRolesAsync(orgId, Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyPermissions);

        var first = await resolver.ResolveAsync(firstToken, CancellationToken.None);
        var second = await resolver.ResolveAsync(secondToken, CancellationToken.None);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.ProviderId.ShouldBe(AuthProviderId.Oidc);
        second.ProviderId.ShouldBe(AuthProviderId.Oidc);

        // Cache hit on the second resolve — the resolver's (iss →
        // provider) map is served from IMemoryCache, so the
        // resolver's own reader call disappears on the second
        // pass. The dispatched ExternalOidcAuthProvider still
        // does its own per-call reader hit (one per ResolveAsync),
        // so the total is 1 (resolver first resolve) + 2 (OIDC
        // provider across both resolves) = 3.
        await configReader.Received(3).GetByOidcIssuerAsync(
            TestOidcIssuer, Arg.Any<CancellationToken>());
    }

    /// <summary>Holder for the NSubstitute mocks wired into both
    /// provider concretes. Centralised so every test sees the same
    /// stub instances.</summary>
    /// <param name="Signing"></param>
    /// <param name="UserLookup"></param>
    /// <param name="RoleResolver"></param>
    /// <param name="PermissionResolver"></param>
    /// <param name="JwksFetcher"></param>
    private sealed record NSubstituteMocks(
        IJwtSigningService Signing,
        IUserLookup UserLookup,
        IRoleResolver RoleResolver,
        IPermissionResolver PermissionResolver,
        IJwksFetcher JwksFetcher);

    /// <summary>
    ///     Build a <see cref="VerifyResult.Success" /> whose principal
    ///     carries the standard <c>sub</c> / <c>tid</c> claims
    ///     (<paramref name="userId" /> / <paramref name="orgId" />).
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="orgId"></param>
    private static VerifyResult.Success BuildSigilSuccess(Guid userId, Guid orgId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(IdentityClaims.UserId, userId.ToString()),
                new Claim(IdentityClaims.TenantId, orgId.ToString()),
            ],
            authenticationType: "TestSigning");
        return new VerifyResult.Success(new ClaimsPrincipal(identity));
    }

    /// <summary>
    ///     Mint an unsigned JWT for tests where the resolver only
    ///     peeks the header (no signature validation happens at the
    ///     dispatch layer — the provider validates). Audience and
    ///     subject are optional; <paramref name="issuer" /> is
    ///     required because that's what the resolver keys off.
    /// </summary>
    /// <param name="issuer"></param>
    /// <param name="audience"></param>
    /// <param name="subject"></param>
    private static string MintUnsignedJwt(
        string issuer,
        string? audience = null,
        string? subject = null)
    {
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = subject is null
                ? null
                : new ClaimsIdentity([new Claim("sub", subject)]),
            Expires = DateTime.UtcNow.AddMinutes(10),
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
        };
        return handler.CreateToken(descriptor);
    }

    /// <summary>Mint an RSA-signed JWT (the OIDC provider actually
    /// validates the signature, so the test must use a real key).</summary>
    private static (RsaSecurityKey Key, JsonWebKey PublicJwk) CreateSigningKey()
    {
        var rsa = System.Security.Cryptography.RSA.Create(2048);
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

    private static string MintRsaSignedJwt(
        SecurityKey signingKey,
        string issuer,
        string audience,
        string subject,
        DateTime expiresAtUtc,
        params Claim[] extraClaims)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim("sub", subject) }.Concat(extraClaims),
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
}
