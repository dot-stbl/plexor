// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BearerAuthenticationHandlerShould — exercise the JWT bearer scheme
// against synthetic HttpContexts. Uses NSubstitute to fake the
// IJwtSigningService contract (no real keypair, no DB).
// ============================================================================

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Auth;

/// <summary>
///     Behavioural tests for <see cref="BearerAuthenticationHandler" />.
///     Each test wires a real <see cref="DefaultHttpContext" />, a real
///     <see cref="TestOptionsMonitor{TOptions}" />, a stub URL encoder,
///     and a fake <see cref="IJwtSigningService" />. The handler
///     constructor matches the framework's DI shape
///     (<c>IOptionsMonitor&lt;BearerOptions&gt;</c>,
///     <c>ILoggerFactory</c>, <c>UrlEncoder</c>) so we mirror that.
/// </summary>
public sealed class BearerAuthenticationHandlerShould
{
    private static async Task<BearerAuthenticationHandler> BuildHandlerAsync(
        HttpContext context,
        IJwtSigningService signing)
    {
        var optionsMonitor = new TestOptionsMonitor<BearerOptions>(new BearerOptions());
        var loggerFactory = NullLoggerFactory.Instance;
        var urlEncoder = UrlEncoder.Default;

        var scheme = new AuthenticationScheme(
            BearerOptions.SchemeName,
            BearerOptions.SchemeName,
            typeof(BearerAuthenticationHandler));

        var handler = new BearerAuthenticationHandler(
            optionsMonitor, loggerFactory, urlEncoder, signing);
        await handler.InitializeAsync(scheme, context);
        return handler;
    }

    /// <summary>Verifies that an absent Authorization header yields NoResult
    /// and that the signing service is never called.</summary>
    [Fact(DisplayName = "Given no Authorization header, when authenticating, then returns NoResult")]
    public async Task NoHeaderReturnsNoResult()
    {
        var signing = Substitute.For<IJwtSigningService>();
        var context = new DefaultHttpContext();
        var handler = await BuildHandlerAsync(context, signing);

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
        await signing.DidNotReceive().VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that a non-Bearer Authorization scheme is treated
    /// as no auth (not a failure) so other handlers can pick up Basic etc.</summary>
    [Fact(DisplayName = "Given wrong scheme header, when authenticating, then returns NoResult")]
    public async Task WrongSchemeReturnsNoResult()
    {
        var signing = Substitute.For<IJwtSigningService>();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";
        var handler = await BuildHandlerAsync(context, signing);

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
        await signing.DidNotReceive().VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies that a VerifyResult.Malformed from the signing
    /// service surfaces as AuthenticateResult.Fail with the same reason.</summary>
    [Fact(DisplayName = "Given malformed JWT, when authenticating, then returns Fail")]
    public async Task MalformedJwtReturnsFail()
    {
        var signing = Substitute.For<IJwtSigningService>();
        signing.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Malformed("base64 decode failed"));
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer garbage";
        var handler = await BuildHandlerAsync(context, signing);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("base64 decode failed");
    }

    /// <summary>Verifies that a VerifyResult.Invalid from the signing
    /// service surfaces as AuthenticateResult.Fail with the same reason.</summary>
    [Fact(DisplayName = "Given invalid JWT, when authenticating, then returns Fail")]
    public async Task InvalidJwtReturnsFail()
    {
        var signing = Substitute.For<IJwtSigningService>();
        signing.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Invalid("signature mismatch"));
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer eyJhbGc.sig";
        var handler = await BuildHandlerAsync(context, signing);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("signature mismatch");
    }

    /// <summary>Verifies that a VerifyResult.Success carries the principal
    /// returned by the signing service and exposes its identity on the result.</summary>
    [Fact(DisplayName = "Given valid JWT, when authenticating, then returns Success with principal identity")]
    public async Task ValidJwtReturnsSuccess()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "user-123")],
            BearerOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);

        var signing = Substitute.For<IJwtSigningService>();
        signing.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Success(principal));

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer eyJhbGc.payload.sig";
        var handler = await BuildHandlerAsync(context, signing);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
        result.Principal!.Identity!.Name.ShouldBe("user-123");
        result.Principal.Identity.AuthenticationType.ShouldBe(BearerOptions.SchemeName);
    }

    /// <summary>Verifies that iat/exp claims from a successful JWT are
    /// surfaced on AuthenticationProperties.IssuedUtc / ExpiresUtc.</summary>
    [Fact(DisplayName = "Given JWT with iat and exp claims, when authenticating, then AuthenticationProperties carries issued and expires instants")]
    public async Task ValidJwtSurfacesIssuedAndExpiresUtc()
    {
        var issuedEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresEpoch = issuedEpoch + 900; // 15-minute lifetime, matches IJwtSigningService.AccessTokenLifetime
        var identity = new ClaimsIdentity(
            [
                new Claim("iat", issuedEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("exp", expiresEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ],
            BearerOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);

        var signing = Substitute.For<IJwtSigningService>();
        signing.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Success(principal));

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer eyJhbGc.payload.sig";
        var handler = await BuildHandlerAsync(context, signing);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
        result.Properties.ShouldNotBeNull();
        result.Properties!.IssuedUtc.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(issuedEpoch));
        result.Properties.ExpiresUtc.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(expiresEpoch));
    }

    /// <summary>Verifies that an Authorization header with multiple
    /// values does not silently splice them together — the first value
    /// is treated as the only one and any subsequent values are ignored.</summary>
    [Fact(DisplayName = "Given multi-valued Authorization header, when authenticating, then only the first value is considered")]
    public async Task MultiValuedAuthorizationHeaderUsesFirstValueOnly()
    {
        var signing = Substitute.For<IJwtSigningService>();
        signing.VerifyAsync("only-this-token", Arg.Any<CancellationToken>())
            .Returns(new VerifyResult.Invalid("ignored"));

        var context = new DefaultHttpContext();
        // Two Authorization headers, comma-joined value: must use only the first.
        context.Request.Headers.Append("Authorization", "Bearer only-this-token");
        context.Request.Headers.Append("Authorization", "Bearer should-be-ignored");
        var handler = await BuildHandlerAsync(context, signing);

        var result = await handler.AuthenticateAsync();

        await signing.Received(1).VerifyAsync("only-this-token", Arg.Any<CancellationToken>());
        result.Failure!.Message.ShouldBe("ignored");
    }

    /// <summary>Verifies that a challenge writes the standard
    /// <c>WWW-Authenticate: Bearer realm="plexor"</c> header and 401 status.</summary>
    [Fact(DisplayName = "Given no auth on a protected endpoint, when challenging, then writes WWW-Authenticate header")]
    public async Task ChallengeWritesRealmHeader()
    {
        var signing = Substitute.For<IJwtSigningService>();
        var context = new DefaultHttpContext();
        var handler = await BuildHandlerAsync(context, signing);

        await handler.ChallengeAsync(new AuthenticationProperties());

        context.Response.Headers.WWWAuthenticate.ToString()
            .ShouldBe($"{BearerOptions.SchemeName} realm=\"plexor\"");
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private sealed class TestOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
        where TOptions : class
    {
        public TOptions CurrentValue
        {
            get { return value; }
        }

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