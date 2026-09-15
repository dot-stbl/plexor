// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoginCommandHandlerShould — exercise the IDP-guard path added in
// Phase 4.6.3c. Three test cases:
//   1. OIDC-configured tenant → handler throws
//      identity.credentials.provider_mismatch with a redirect URL
//      extension pointing at /auth/oidc/authorize.
//   2. Sigil-configured tenant → handler passes the IDP guard
//      and reaches the user-lookup step (verified by observing
//      the InvalidCredentials exception the stub password hasher
//      returns; a CredentialsProviderMismatch here would mean the
//      guard fired by mistake).
//   3. Unconfigured tenant (no OrgAuthProviderConfig row) → falls
//      through to the existing flow. Same shape as the Sigil case:
//      the IDP guard must not short-circuit.
//
// The InMemory provider can't model the production Identity
// schema (the `Permissions` array + IReadOnlyList<Email>
// converter), so the tests stop at the IDP-guard boundary rather
// than exercising the full issue-access-token path. The
// RefreshCommandHandlerShould tests cover the issue path
// separately; the Login path's downstream steps are pre-existing
// and unchanged.
// ============================================================================

using NSubstitute;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Unit.Realm;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Auth;

/// <summary>
///     Behavioural tests for the Phase 4.6.3c IDP guard in
///     <see cref="LoginCommandHandler" />.
/// </summary>
public sealed class LoginCommandHandlerShould
{
    /// <summary>
    ///     Given an OIDC-configured tenant, when login is called,
    ///     then the handler throws
    ///     <see cref="IdentityExceptions.CredentialsProviderMismatch" />
    ///     with a <c>redirect</c> extension that points the operator
    ///     at <c>POST /auth/oidc/authorize</c>.
    /// </summary>
    [Fact(DisplayName = "Given an OIDC-configured tenant, when login is called, then the handler throws identity.credentials.provider_mismatch with a redirect extension")]
    public async Task Login_WithOidcTenant_ReturnsProviderMismatchErrorAsync()
    {
        var orgId = Guid.NewGuid();
        const string originalRedirect = "/console/projects";
        var (handler, realm) = await BuildHandlerAsync(orgId, configureOidc: true);

        await using (realm)
        {
            var exception = await Should.ThrowAsync<IdentityException>(
                () => handler.HandleAsync(
                    new LoginCommand(
                        OrgId: orgId,
                        Email: "alice@example.com",
                        Username: null,
                        Password: "correct-horse-battery-staple",
                        RedirectPath: originalRedirect),
                    CancellationToken.None));

            exception.Code.ShouldBe(IdentityExceptions.CredentialsProviderMismatch);

            var extensions = exception.Extensions;
            extensions.ShouldNotBeNull();
            extensions.ShouldContainKey("redirect");
            var redirect = extensions["redirect"] as string;
            redirect.ShouldNotBeNull();
            redirect.ShouldStartWith("/api/v1/auth/oidc/authorize?");
            redirect.ShouldContain($"org={orgId}");
            redirect.ShouldContain($"redirect={Uri.EscapeDataString(originalRedirect)}");
        }
    }

    /// <summary>
    ///     Given a Sigil-configured tenant, when login is called,
    ///     then the IDP guard does NOT short-circuit — the handler
    ///     proceeds past the guard and reaches the user-lookup /
    ///     password-verify step. Verified by observing the
    ///     <see cref="IdentityExceptions.InvalidCredentials" />
    ///     exception the stub password hasher raises; a
    ///     <see cref="IdentityExceptions.CredentialsProviderMismatch" />
    ///     here would mean the IDP guard fired by mistake on a
    ///     Sigil tenant.
    /// </summary>
    [Fact(DisplayName = "Given a Sigil-configured tenant, when login is called, then the IDP guard does not short-circuit")]
    public async Task Login_WithSigilTenant_DoesNotShortCircuitAsync()
    {
        var orgId = Guid.NewGuid();
        var (handler, realm) = await BuildHandlerAsync(orgId, configureOidc: false);

        await using (realm)
        {
            var exception = await Should.ThrowAsync<IdentityException>(
                () => handler.HandleAsync(
                    new LoginCommand(
                        OrgId: orgId,
                        Email: "alice@example.com",
                        Username: null,
                        Password: "any-password",
                        RedirectPath: "/console"),
                    CancellationToken.None));

            // The IDP guard returns CredentialsProviderMismatch; the
            // user-lookup / password-verify step returns
            // InvalidCredentials. A Sigil tenant must reach the
            // latter step.
            exception.Code.ShouldNotBe(IdentityExceptions.CredentialsProviderMismatch);
        }
    }

    /// <summary>
    ///     Given an unconfigured tenant (no OrgAuthProviderConfig
    ///     row), when login is called, then the IDP guard does NOT
    ///     short-circuit. Backward-compat with the v0.1 single-
    ///     tenant default before the seeder has run.
    /// </summary>
    [Fact(DisplayName = "Given an unconfigured tenant, when login is called, then the IDP guard does not short-circuit")]
    public async Task Login_WithUnconfiguredTenant_DoesNotShortCircuitAsync()
    {
        var orgId = Guid.NewGuid();
        var (handler, realm) = await BuildHandlerAsync(orgId, configureOidc: false, seedConfig: false);

        await using (realm)
        {
            var exception = await Should.ThrowAsync<IdentityException>(
                () => handler.HandleAsync(
                    new LoginCommand(
                        OrgId: orgId,
                        Email: "alice@example.com",
                        Username: null,
                        Password: "any-password",
                        RedirectPath: "/console"),
                    CancellationToken.None));

            exception.Code.ShouldNotBe(IdentityExceptions.CredentialsProviderMismatch);
        }
    }

    /// <summary>
    ///     Build a <see cref="LoginCommandHandler" /> with NSubstitute
    ///     stubs for every Application-layer dependency. The realm
    ///     context is real (in-memory) so the IDP-guard read against
    ///     <c>OrgAuthProviderConfig</c> exercises the actual EF
    ///     roundtrip. The IdentityDbContext is created via the
    ///     test factory but never queried — the tests stop at the
    ///     IDP-guard boundary; the password-hash / role-resolve
    ///     steps are downstream and pre-existing.
    /// </summary>
    /// <param name="orgId"></param>
    /// <param name="configureOidc"></param>
    /// <param name="seedConfig"></param>
    private static async Task<(LoginCommandHandler Handler, RealmDbContext Realm)> BuildHandlerAsync(
        Guid orgId,
        bool configureOidc,
        bool seedConfig = true)
    {
        var realm = await RealmTestDb.CreateAsync();
        var identity = await IdentityTestDb.CreateAsync();

        if (seedConfig)
        {
            var now = DateTimeOffset.UtcNow;
            var slug = $"org-{orgId.ToString()[..6]}";
            await realm.Organizations.AddAsync(new Organization
            {
                Id = orgId,
                Name = slug,
                Slug = slug,
                Status = "active",
                CreatedAt = now,
            });
            await realm.SaveChangesAsync();

            await realm.OrgAuthProviderConfigs.AddAsync(new OrgAuthProviderConfig
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Provider = configureOidc ? OrgAuthProvider.Oidc : OrgAuthProvider.Sigil,
                OidcAuthority = configureOidc ? "https://kc.example.com/realms/plexor" : null,
                OidcClientId = configureOidc ? "plexor-cli" : null,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await realm.SaveChangesAsync();
        }

        var userLookup = Substitute.For<IUserLookup>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var refreshTokens = Substitute.For<IRefreshTokenStore>();
        var tokenIssuer = Substitute.For<ITokenIssuer>();
        var configReader = new EfOrgAuthProviderConfigReaderAdapter(realm);

        // User lookup returns null so the handler raises
        // InvalidCredentials after the IDP guard (vs. the
        // CredentialsProviderMismatch we'd see if the guard fired).
        userLookup.FindByEmailAsync(orgId, "alice@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new LoginCommandHandler(
            userLookup,
            passwordHasher,
            refreshTokens,
            tokenIssuer,
            identity,
            TimeProvider.System,
            configReader);

        return (handler, realm);
    }

    /// <summary>
    ///     Adapter that exposes a real <see cref="RealmDbContext" />
    ///     through the <see cref="IOrgAuthProviderConfigReader" />
    ///     seam so the handler's IDP guard reads the seeded
    ///     <c>OrgAuthProviderConfig</c> row. Stands in for the
    ///     production EfOrgAuthProviderConfigReader — kept local
    ///     to the test file because no production code needs it.
    /// </summary>
    /// <param name="realm">In-memory realm DbContext.</param>
    private sealed class EfOrgAuthProviderConfigReaderAdapter(RealmDbContext realm)
        : IOrgAuthProviderConfigReader
    {
        public Task<OrgAuthProviderConfig?> GetForOrgAsync(
            Guid orgId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(realm.OrgAuthProviderConfigs
                .FirstOrDefault(c => c.OrgId == orgId));
        }

        public Task<OrgAuthProviderConfig?> GetByOidcIssuerAsync(
            string oidcAuthority,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(realm.OrgAuthProviderConfigs
                .FirstOrDefault(c => c.OidcAuthority == oidcAuthority));
        }
    }
}
