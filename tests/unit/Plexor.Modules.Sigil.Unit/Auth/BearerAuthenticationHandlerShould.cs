// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BearerAuthenticationHandlerShould — exercise the JWT bearer scheme
// against synthetic HttpContexts. The handler now delegates JWT
// verification to IAuthProviderResolver (Phase 4.6.2c); the API-key
// branch keeps IApiKeyAuthenticationService. Tests stub the resolver
// (not IJwtSigningService) so the dispatch boundary is what's under
// test.
// ============================================================================

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Auth;

/// <summary>
///     Behavioural tests for <see cref="BearerAuthenticationHandler" />.
///     Each test wires a real <see cref="DefaultHttpContext" />, a real
///     <see cref="TestOptionsMonitor{TOptions}" />, a stub URL encoder,
///     a stub <see cref="IAuthProviderResolver" />, and a stub
///     <see cref="IApiKeyAuthenticationService" />. The handler
///     constructor matches the framework's DI shape
///     (<c>IOptionsMonitor&lt;BearerOptions&gt;</c>,
///     <c>ILoggerFactory</c>, <c>UrlEncoder</c>) so we mirror that.
/// </summary>
public sealed class BearerAuthenticationHandlerShould
{
    private static async Task<BearerAuthenticationHandler> BuildHandlerAsync(
        HttpContext context,
        IAuthProviderResolver resolver,
        IApiKeyAuthenticationService apiKeys)
    {
        var optionsMonitor = new TestOptionsMonitor<BearerOptions>(new BearerOptions());
        var loggerFactory = NullLoggerFactory.Instance;
        var urlEncoder = UrlEncoder.Default;

        var scheme = new AuthenticationScheme(
            BearerOptions.SchemeName,
            BearerOptions.SchemeName,
            typeof(BearerAuthenticationHandler));

        var handler = new BearerAuthenticationHandler(
            optionsMonitor, loggerFactory, urlEncoder, resolver, apiKeys);
        await handler.InitializeAsync(scheme, context);
        return handler;
    }

    /// <summary>Verifies that an absent Authorization header yields
    /// NoResult and that the resolver is never called.</summary>
    [Fact(DisplayName = "Given no Authorization header, when authenticating, then returns NoResult")]
    public async Task NoHeaderReturnsNoResultAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        var context = new DefaultHttpContext();
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
        await resolver.DidNotReceive().ResolveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that a non-Bearer Authorization scheme is treated
    /// as no auth (not a failure) so other handlers can pick up Basic etc.</summary>
    [Fact(DisplayName = "Given wrong scheme header, when authenticating, then returns NoResult")]
    public async Task WrongSchemeReturnsNoResultAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
        await resolver.DidNotReceive().ResolveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that an Authorization header with only the
    /// Bearer prefix and an empty credential yields Fail (not
    /// NoResult — the client attempted auth but failed).</summary>
    [Fact(DisplayName = "Given an empty Bearer token, when authenticating, then returns Fail")]
    public async Task EmptyBearerTokenReturnsFailAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer ";
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        await resolver.DidNotReceive().ResolveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that a JWT-shaped Bearer token whose
    /// resolution returns null maps to Fail with the standard
    /// <c>invalid_token</c> reason.</summary>
    [Fact(DisplayName = "Given a JWT Bearer token that the resolver rejects, when authenticating, then returns Fail")]
    public async Task JwtResolverRejectionReturnsFailAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AuthResolution?)null);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer eyJhbGc.payload.sig";
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("invalid_token");
        await resolver.Received(1).ResolveAsync(
            "eyJhbGc.payload.sig", Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that a JWT-shaped Bearer token whose
    /// resolution succeeds maps to Success and that the resulting
    /// principal carries the canonical Sigil claim shape
    /// (<c>sub</c> / <c>tid</c> / <c>iss</c> / <c>role</c>[] /
    /// <c>permission</c>[]).</summary>
    [Fact(DisplayName = "Given a valid JWT Bearer token, when authenticating, then returns Success with the canonical Sigil claim shape")]
    public async Task ValidJwtReturnsSuccessWithCanonicalClaimsAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resolution = new AuthResolution(
            OrgId: orgId,
            UserId: userId,
            ProviderId: AuthProviderId.Sigil,
            IsService: false,
            Roles: ["admin"],
            Permissions: ["vms.read", "vms.write"],
            TokenLifetime: TimeSpan.FromMinutes(15));
        var resolver = Substitute.For<IAuthProviderResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(resolution);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer eyJhbGc.payload.sig";
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
        result.Principal!.Identity!.AuthenticationType.ShouldBe(BearerOptions.SchemeName);

        // Claim shape parity with v0.5 — read by HttpContextCurrentUser
        // and RequirePermissionAttribute. Drift here breaks the
        // authorization pipeline silently.
        result.Principal.FindFirst(IdentityClaims.UserId)?.Value.ShouldBe(userId.ToString());
        result.Principal.FindFirst(IdentityClaims.TenantId)?.Value.ShouldBe(orgId.ToString());
        result.Principal.FindFirst(IdentityClaims.Issuer)?.Value.ShouldBe("sigil");
        result.Principal.FindFirst(IdentityClaims.IsService)?.Value.ShouldBe("false");
        result.Principal.FindAll(IdentityClaims.Roles)
            .Select(static claim => claim.Value)
            .ShouldBe(["admin"]);
        result.Principal.FindAll(IdentityClaims.Permission)
            .Select(static claim => claim.Value)
            .ShouldBe(["vms.read", "vms.write"]);
    }

    /// <summary>Verifies that an OIDC-issued JWT (resolver returns an
    /// Oidc-tagged resolution) lands a principal with
    /// <c>iss="oidc"</c> on the claim.</summary>
    [Fact(DisplayName = "Given an OIDC-resolved JWT, when authenticating, then the principal's iss claim carries the Oidc discriminator")]
    public async Task ValidOidcJwtReturnsPrincipalWithOidcIssuerClaimAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resolution = new AuthResolution(
            OrgId: orgId,
            UserId: userId,
            ProviderId: AuthProviderId.Oidc,
            IsService: false,
            Roles: ["viewer"],
            Permissions: ["*.read"],
            TokenLifetime: TimeSpan.FromHours(1));
        var resolver = Substitute.For<IAuthProviderResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(resolution);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer eyJhbGc.payload.sig";
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
        result.Principal.FindFirst(IdentityClaims.Issuer)?.Value.ShouldBe("oidc");
    }

    /// <summary>Verifies that an Authorization header with multiple
    /// values does not silently splice them together — the first
    /// value is treated as the only one and any subsequent values
    /// are ignored.</summary>
    [Fact(DisplayName = "Given multi-valued Authorization header, when authenticating, then only the first value is considered")]
    public async Task MultiValuedAuthorizationHeaderUsesFirstValueOnlyAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        resolver.ResolveAsync("only-this-token.sig", Arg.Any<CancellationToken>())
            .Returns((AuthResolution?)null);
        var context = new DefaultHttpContext();
        // Two Authorization headers, comma-joined value: must use only the first.
        context.Request.Headers.Append("Authorization", "Bearer only-this-token.sig");
        context.Request.Headers.Append("Authorization", "Bearer should-be-ignored.payload.sig");
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        await resolver.Received(1).ResolveAsync(
            "only-this-token.sig", Arg.Any<CancellationToken>());
        result.Failure!.Message.ShouldBe("invalid_token");
    }

    /// <summary>Verifies that an API-key Bearer token
    /// (<c>kid_xxx.&lt;secret&gt;</c>) routes through
    /// <see cref="IApiKeyAuthenticationService" /> rather than the
    /// resolver — the API-key branch is unchanged from v0.5.</summary>
    [Fact(DisplayName = "Given an API-key Bearer token, when authenticating, then dispatches to the API-key service without consulting the resolver")]
    public async Task ApiKeyTokenRoutesToApiKeyServiceAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var identity = new ClaimsIdentity(
            [new Claim(IdentityClaims.UserId, Guid.NewGuid().ToString())],
            authenticationType: "PlexorApiKey");
        var apiPrincipal = new ClaimsPrincipal(identity);
        apiKeys.AuthenticateAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new ApiKeyAuthenticationResult.Success(apiPrincipal, Guid.NewGuid()));
        var context = new DefaultHttpContext();
        var keyId = Guid.NewGuid();
        context.Request.Headers.Authorization = $"Bearer kid_{keyId}.<secret>";
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
        await apiKeys.Received(1).AuthenticateAsync(
            keyId, "<secret>", Arg.Any<CancellationToken>());
        await resolver.DidNotReceive().ResolveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that a malformed API-key Bearer token
    /// (prefix without dot + secret) returns Fail without
    /// consulting the API-key service.</summary>
    [Fact(DisplayName = "Given a malformed API-key Bearer token, when authenticating, then returns Fail without consulting the API-key service")]
    public async Task MalformedApiKeyTokenReturnsFailAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var context = new DefaultHttpContext();
        // kid_ prefix present, but no dot separator and no guid.
        context.Request.Headers.Authorization = "Bearer kid_not-a-guid";
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("Malformed API key.");
        await apiKeys.DidNotReceive().AuthenticateAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that a challenge writes the standard
    /// <c>WWW-Authenticate: Bearer realm="plexor", error="invalid_token"</c>
    /// header (RFC 6750 §3) and 401 status. error_description is
    /// intentionally omitted to avoid information leakage.</summary>
    [Fact(DisplayName = "Given no auth on a protected endpoint, when challenging, then writes WWW-Authenticate header")]
    public async Task ChallengeWritesRealmHeaderAsync()
    {
        var resolver = Substitute.For<IAuthProviderResolver>();
        var context = new DefaultHttpContext();
        var apiKeys = Substitute.For<IApiKeyAuthenticationService>();
        var handler = await BuildHandlerAsync(context, resolver, apiKeys);

        await handler.ChallengeAsync(new AuthenticationProperties());

        context.Response.Headers.WWWAuthenticate.ToString()
            .ShouldBe($"{BearerOptions.SchemeName} realm=\"plexor\", error=\"invalid_token\"");
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private sealed class TestOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
        where TOptions : class
    {
        public TOptions CurrentValue => value;

        public TOptions Get(string? name)
        {
            return value;
        }

        public IDisposable? OnChange(Action<TOptions, string?> listener)
        {
            return null;
        }
    }
}
