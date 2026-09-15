// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigilAuthProviderShould — exercise the local-email+password provider
// against NSubstitute mocks for the JWT signer + user lookup + role
// resolver + permission resolver, plus a real in-memory RealmDbContext
// for the per-tenant routing decision. Covers the resolution success
// path, every null-return path, and the CanAuthenticateForAsync gate.
// ============================================================================

using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers;
using Plexor.Modules.Sigil.Unit.Realm;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="SigilAuthProvider" />. The
///     provider is a thin orchestrator — the value lives in the
///     decision tree (verify → tenant gate → user gate → resolve
///     roles + permissions), not in any single sub-step.
///     NSubstitute stubs every Application-layer dependency so the
///     decision tree is the only thing under test.
/// </summary>
public sealed class SigilAuthProviderShould
{
    private static readonly TimeSpan ExpectedTokenLifetime = TimeSpan.FromMinutes(15);

    private static readonly string[] EmptyRoles = [];

    private static readonly string[] EmptyPermissions = [];

    private static readonly string[] AdminRole = ["admin"];

    private static readonly string[] AdminWildcardPermissions = ["*"];

    /// <summary>
    ///     Build a fake <see cref="VerifyResult.Success" /> whose
    ///     principal carries the standard <c>sub</c> / <c>tid</c>
    ///     claims. <c>sub</c> = <paramref name="userId" />,
    ///     <c>tid</c> = <paramref name="orgId" />.
    /// </summary>
    /// <param name="userId">Sigil user id to embed in the <c>sub</c> claim.</param>
    /// <param name="orgId">Tenant id to embed in the <c>tid</c> claim.</param>
    private static VerifyResult.Success BuildSuccess(Guid userId, Guid orgId)
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
    ///     Wire a SigilAuthProvider with the supplied mocks + a
    ///     real <see cref="RealmDbContext" />. The default config
    ///     table is empty — callers seed it before invoking
    ///     <c>ResolveAsync</c> when they want to test the OIDC
    ///     rejection path.
    /// </summary>
    /// <param name="signing">Stubbed JWT signer.</param>
    /// <param name="userLookup">Stubbed user lookup.</param>
    /// <param name="roleResolver">Stubbed role resolver.</param>
    /// <param name="permissionResolver">Stubbed permission resolver.</param>
    /// <param name="realm">In-memory realm DbContext.</param>
    private static (SigilAuthProvider Provider, RealmDbContext Realm) BuildProvider(
        IJwtSigningService signing,
        IUserLookup userLookup,
        IRoleResolver roleResolver,
        IPermissionResolver permissionResolver,
        RealmDbContext realm)
    {
        var provider = new SigilAuthProvider(
            signing,
            userLookup,
            roleResolver,
            permissionResolver,
            realm,
            NullLogger<SigilAuthProvider>.Instance);
        return (provider, realm);
    }

    /// <summary>Given a Sigil-issued token, when the tenant has no
    /// OrgAuthProviderConfig row, when resolving, then returns a
    /// populated AuthResolution.</summary>
    [Fact(DisplayName = "Given a valid Sigil token and an unconfigured tenant, when resolving, then returns a populated AuthResolution")]
    public async Task ResolveAsync_WithValidTokenAndUnconfiguredTenant_ReturnsResolutionAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(BuildSuccess(userId, orgId));
        userLookup.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = userId,
                OrgId = orgId,
                Email = new Email("alice@example.com"),
                DisplayName = "Alice",
                Status = "active",
            });
        roleResolver.ResolveAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(AdminRole);
        permissionResolver.ResolveAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(AdminWildcardPermissions);
        await using var realm = await RealmTestDb.CreateAsync();

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldNotBeNull();
        resolution.OrgId.ShouldBe(orgId);
        resolution.UserId.ShouldBe(userId);
        resolution.ProviderId.ShouldBe(AuthProviderId.Sigil);
        resolution.IsService.ShouldBeFalse();
        resolution.Roles.ShouldBe(AdminRole);
        resolution.Permissions.ShouldBe(AdminWildcardPermissions);
        resolution.TokenLifetime.ShouldBe(ExpectedTokenLifetime);
    }

    /// <summary>Given a Sigil-issued token, when the tenant is configured
    /// for Oidc (an admin flipped the row), when resolving, then returns
    /// null.</summary>
    [Fact(DisplayName = "Given a Sigil token for an OIDC-configured tenant, when resolving, then returns null")]
    public async Task ResolveAsync_WithTokenForOidcTenant_ReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(BuildSuccess(userId, orgId));
        await using var realm = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(realm, orgId);
        await SeedOrgAuthProviderAsync(realm, orgId, OrgAuthProvider.Oidc);

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldBeNull();
        await userLookup.DidNotReceive().FindByIdAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given a Sigil-issued token, when the tenant is
    /// explicitly configured for Sigil, when resolving, then returns a
    /// populated AuthResolution.</summary>
    [Fact(DisplayName = "Given a Sigil token for an explicitly Sigil-configured tenant, when resolving, then returns a populated AuthResolution")]
    public async Task ResolveAsync_WithTokenForSigilTenant_ReturnsResolutionAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(BuildSuccess(userId, orgId));
        userLookup.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = userId,
                OrgId = orgId,
                Email = new Email("bob@example.com"),
                Status = "active",
            });
        roleResolver.ResolveAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(EmptyRoles);
        permissionResolver.ResolveAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(EmptyPermissions);
        await using var realm = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(realm, orgId);
        await SeedOrgAuthProviderAsync(realm, orgId, OrgAuthProvider.Sigil);

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldNotBeNull();
        resolution.ProviderId.ShouldBe(AuthProviderId.Sigil);
        resolution.OrgId.ShouldBe(orgId);
    }

    /// <summary>Given an invalid signature, when resolving, then returns
    /// null and never touches the user lookup or tenant config.</summary>
    [Fact(DisplayName = "Given an invalid signature, when resolving, then returns null without consulting the user lookup")]
    public async Task ResolveAsync_WithInvalidSignature_ReturnsNullAsync()
    {
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Invalid("signature mismatch"));
        await using var realm = await RealmTestDb.CreateAsync();

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldBeNull();
        await userLookup.DidNotReceive().FindByIdAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await roleResolver.DidNotReceive().ResolveAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await permissionResolver.DidNotReceive().ResolveAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given a malformed JWT, when resolving, then returns null.</summary>
    [Fact(DisplayName = "Given a malformed JWT, when resolving, then returns null")]
    public async Task ResolveAsync_WithMalformedJwt_ReturnsNullAsync()
    {
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Malformed("not three dots"));
        await using var realm = await RealmTestDb.CreateAsync();

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given a valid token, when the user lookup returns
    /// null (user deleted between issue and verify), when resolving,
    /// then returns null.</summary>
    [Fact(DisplayName = "Given a valid token but a deleted user, when resolving, then returns null")]
    public async Task ResolveAsync_WithUnknownUser_ReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(BuildSuccess(userId, orgId));
        userLookup.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        await using var realm = await RealmTestDb.CreateAsync();

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldBeNull();
        await roleResolver.DidNotReceive().ResolveAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Given a valid token, when the user is suspended (status
    /// != "active"), when resolving, then returns null.</summary>
    [Fact(DisplayName = "Given a valid token but a suspended user, when resolving, then returns null")]
    public async Task ResolveAsync_WithSuspendedUser_ReturnsNullAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(BuildSuccess(userId, orgId));
        userLookup.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = userId,
                OrgId = orgId,
                Email = new Email("charlie@example.com"),
                Status = "suspended",
            });
        await using var realm = await RealmTestDb.CreateAsync();

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given a valid token missing the standard <c>sub</c> claim,
    /// when resolving, then returns null.</summary>
    [Fact(DisplayName = "Given a valid token missing the sub claim, when resolving, then returns null")]
    public async Task ResolveAsync_WithMissingSubClaim_ReturnsNullAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(IdentityClaims.TenantId, Guid.NewGuid().ToString())],
            authenticationType: "TestSigning");
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        signing.VerifyAsync("token", Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Success(new ClaimsPrincipal(identity)));
        await using var realm = await RealmTestDb.CreateAsync();

        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var resolution = await provider.ResolveAsync("token", CancellationToken.None);

        resolution.ShouldBeNull();
    }

    /// <summary>Given an unconfigured tenant (v0.1 single-tenant default),
    /// when CanAuthenticateForAsync is called, then returns true.</summary>
    [Fact(DisplayName = "Given an unconfigured tenant, when CanAuthenticateForAsync runs, then returns true")]
    public async Task CanAuthenticateForAsync_WithUnconfiguredTenant_ReturnsTrueAsync()
    {
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        await using var realm = await RealmTestDb.CreateAsync();
        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var result = await provider.CanAuthenticateForAsync(
            Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeTrue();
    }

    /// <summary>Given a tenant explicitly configured for Sigil, when
    /// CanAuthenticateForAsync runs, then returns true.</summary>
    [Fact(DisplayName = "Given a Sigil-configured tenant, when CanAuthenticateForAsync runs, then returns true")]
    public async Task CanAuthenticateForAsync_WithSigilConfiguredTenant_ReturnsTrueAsync()
    {
        var orgId = Guid.NewGuid();
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        await using var realm = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(realm, orgId);
        await SeedOrgAuthProviderAsync(realm, orgId, OrgAuthProvider.Sigil);
        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var result = await provider.CanAuthenticateForAsync(
            orgId, CancellationToken.None);

        result.ShouldBeTrue();
    }

    /// <summary>Given a tenant configured for Oidc, when
    /// CanAuthenticateForAsync runs, then returns false.</summary>
    [Fact(DisplayName = "Given an OIDC-configured tenant, when CanAuthenticateForAsync runs, then returns false")]
    public async Task CanAuthenticateForAsync_WithOidcConfiguredTenant_ReturnsFalseAsync()
    {
        var orgId = Guid.NewGuid();
        var (signing, userLookup, roleResolver, permissionResolver) = SubstituteAuthServices();
        await using var realm = await RealmTestDb.CreateAsync();
        await SeedOrgAsync(realm, orgId);
        await SeedOrgAuthProviderAsync(realm, orgId, OrgAuthProvider.Oidc);
        var (provider, _) = BuildProvider(
            signing, userLookup, roleResolver, permissionResolver, realm);

        var result = await provider.CanAuthenticateForAsync(
            orgId, CancellationToken.None);

        result.ShouldBeFalse();
    }

    private static (IJwtSigningService, IUserLookup, IRoleResolver, IPermissionResolver) SubstituteAuthServices()
    {
        return (
            Substitute.For<IJwtSigningService>(),
            Substitute.For<IUserLookup>(),
            Substitute.For<IRoleResolver>(),
            Substitute.For<IPermissionResolver>());
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
        OrgAuthProvider provider)
    {
        var now = DateTimeOffset.UtcNow;
        await db.OrgAuthProviderConfigs.AddAsync(new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Provider = provider,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }
}
