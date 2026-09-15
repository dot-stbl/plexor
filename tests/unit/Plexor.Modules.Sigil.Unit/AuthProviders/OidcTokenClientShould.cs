// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OidcTokenClientShould — exercise the OIDC token-exchange path
// (Phase 4.6.3a). Mocks IOrgAuthProviderConfigReader + the named
// "Plexor-OidcDiscovery" HttpClient factory; uses the in-memory
// data-protection provider to round-trip a ciphertext so the
// secret-unprotect branch is real, not faked.
//
// Coverage:
//   1. Happy path — exchange returns a populated OidcTokenResponse.
//   2. No config — return null without hitting HTTP.
//   3. Sigil config (wrong provider) — return null.
//   4. HTTP 4xx — return null.
//   5. Basic auth header — assert base64(clientId:secret) is set.
//   6. Empty decrypted secret — return null.
//   7. Empty access_token in response — return null.
//   8. Minimal response (no id_token, no token_type) — defaults.
//   9. Posts to {authority}/protocol/openid-connect/token.
// ============================================================================

using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.Modules.Realm.Application.AuthProviders;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.AuthProviders;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="OidcTokenClient" />. Covers
///     every null-return branch + the happy path + the
///     wire-format assertion (HTTP Basic auth on the outbound
///     request carries the decrypted client secret).
/// </summary>
public sealed class OidcTokenClientShould
{
    private const string TestAuthority = "https://kc.example.com/realms/plexor";

    private const string TestClientId = "plexor-console";

    private const string TestClientSecret = "supersecret-oidc-client-secret";

    private const string TestRedirectUri = "https://plexor.example.com/api/v1/auth/oidc/callback";

    private const string TestCodeVerifier = "abcdefghijklmnopqrstuvwxyz0123456789-._~ABCDEFGHIJ";

    private const string TestAuthorizationCode = "auth-code-from-idp";

    private const string TokenEndpointPath = "/protocol/openid-connect/token";

    private static readonly string TokenEndpointUrl =
        new Uri(new Uri(TestAuthority), TokenEndpointPath).ToString();

    private const string TokenResponseJson =
        /*lang=json,strict*/ """{"access_token":"at-123","id_token":"it-456","token_type":"Bearer","expires_in":300,"refresh_token":"rt-789"}""";

    /// <summary>Given an OIDC-configured org with a valid code,
    /// when ExchangeCodeAsync is called, then returns a populated
    /// OidcTokenResponse with the access/refresh/id tokens from the
    /// IDP.</summary>
    [Fact(DisplayName = "Given a valid OIDC config and code, when ExchangeCodeAsync runs, then returns a populated token response")]
    public async Task ExchangeCodeAsync_WithValidConfig_ReturnsTokenResponseAsync()
    {
        var fixture = BuildClient();
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(fixture.Protector));
        fixture.Handler.SetResponse(static _ => ScriptedResponse.Json(HttpStatusCode.OK, TokenResponseJson));

        var response = await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        response.ShouldNotBeNull();
        response.AccessToken.ShouldBe("at-123");
        response.IdToken.ShouldBe("it-456");
        response.TokenType.ShouldBe("Bearer");
        response.ExpiresIn.ShouldBe(300);
        response.RefreshToken.ShouldBe("rt-789");
        fixture.Handler.CallCount.ShouldBe(1);
    }

    /// <summary>Given an org with no OrgAuthProviderConfig row,
    /// when ExchangeCodeAsync is called, then returns null without
    /// issuing an HTTP request.</summary>
    [Fact(DisplayName = "Given no OrgAuthProviderConfig, when ExchangeCodeAsync runs, then returns null without an HTTP call")]
    public async Task ExchangeCodeAsync_WithMissingConfig_ReturnsNullAsync()
    {
        var fixture = BuildClient();
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((OrgAuthProviderConfig?)null);

        var response = await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        response.ShouldBeNull();
        fixture.Handler.CallCount.ShouldBe(0);
    }

    /// <summary>Given an org whose provider is Sigil (not Oidc),
    /// when ExchangeCodeAsync is called, then returns null without
    /// an HTTP request.</summary>
    [Fact(DisplayName = "Given a Sigil provider, when ExchangeCodeAsync runs, then returns null without an HTTP call")]
    public async Task ExchangeCodeAsync_WithSigilProvider_ReturnsNullAsync()
    {
        var fixture = BuildClient();
        var sigilConfig = new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            Provider = OrgAuthProvider.Sigil,
            OidcAuthority = null,
            OidcClientId = null,
            OidcClientSecretProtected = null,
            OidcScopes = ["openid", "profile", "email"],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(sigilConfig);

        var response = await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        response.ShouldBeNull();
        fixture.Handler.CallCount.ShouldBe(0);
    }

    /// <summary>Given the IDP returns an HTTP 4xx, when
    /// ExchangeCodeAsync is called, then returns null.</summary>
    [Fact(DisplayName = "Given an HTTP 4xx from the IDP, when ExchangeCodeAsync runs, then returns null")]
    public async Task ExchangeCodeAsync_WithHttpFailure_ReturnsNullAsync()
    {
        var fixture = BuildClient();
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(fixture.Protector));
        fixture.Handler.SetResponse(static _ => ScriptedResponse.Status(HttpStatusCode.BadRequest));

        var response = await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        response.ShouldBeNull();
        fixture.Handler.CallCount.ShouldBe(1);
    }

    /// <summary>Given the client makes a request, when the request
    /// is dispatched, then the Authorization header is
    /// <c>Basic base64(clientId:secret)</c> where the secret is the
    /// decrypted per-tenant secret (not the protected ciphertext).</summary>
    [Fact(DisplayName = "Given a valid OIDC config, when ExchangeCodeAsync dispatches the HTTP request, then the Authorization header is Basic base64(clientId:decrypted-secret)")]
    public async Task ExchangeCodeAsync_SendsBasicAuthHeaderWithDecryptedSecretAsync()
    {
        var fixture = BuildClient();
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(fixture.Protector));
        fixture.Handler.SetResponse(static _ => ScriptedResponse.Json(HttpStatusCode.OK, TokenResponseJson));

        await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        fixture.Handler.LastRequest.ShouldNotBeNull();
        var auth = fixture.Handler.LastRequest.Headers.Authorization;
        auth.ShouldNotBeNull();
        auth.Scheme.ShouldBe("Basic");
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter!));
        decoded.ShouldBe($"{TestClientId}:{TestClientSecret}");
    }

    /// <summary>Given the per-tenant secret is empty after decrypt
    /// (admin hasn't supplied one), when ExchangeCodeAsync runs,
    /// then returns null without an HTTP call.</summary>
    [Fact(DisplayName = "Given an empty decrypted secret, when ExchangeCodeAsync runs, then returns null without an HTTP call")]
    public async Task ExchangeCodeAsync_WithEmptyDecryptedSecret_ReturnsNullAsync()
    {
        var fixture = BuildClient();
        var config = BuildOidcConfig(fixture.Protector, plaintextSecret: "");
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(config);

        var response = await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        response.ShouldBeNull();
        fixture.Handler.CallCount.ShouldBe(0);
    }

    /// <summary>Given the IDP returns a body without access_token,
    /// when ExchangeCodeAsync runs, then returns null.</summary>
    [Fact(DisplayName = "Given a response body without access_token, when ExchangeCodeAsync runs, then returns null")]
    public async Task ExchangeCodeAsync_WithMissingAccessToken_ReturnsNullAsync()
    {
        var fixture = BuildClient();
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(fixture.Protector));
        fixture.Handler.SetResponse(static _ => ScriptedResponse.Json(
            HttpStatusCode.OK,
            /*lang=json,strict*/ """{"token_type":"Bearer"}"""));

        var response = await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        response.ShouldBeNull();
        fixture.Handler.CallCount.ShouldBe(1);
    }

    /// <summary>Given the IDP returns access_token + token_type
    /// only, when ExchangeCodeAsync runs, then defaults TokenType
    /// to <c>"Bearer"</c> and IdToken to <c>""</c>.</summary>
    [Fact(DisplayName = "Given a minimal token response, when ExchangeCodeAsync runs, then defaults TokenType to Bearer and IdToken to empty")]
    public async Task ExchangeCodeAsync_WithMinimalResponse_DefaultsMissingFieldsAsync()
    {
        var fixture = BuildClient();
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(fixture.Protector));
        fixture.Handler.SetResponse(static _ => ScriptedResponse.Json(
            HttpStatusCode.OK,
            /*lang=json,strict*/ """{"access_token":"at-only","expires_in":60}"""));

        var response = await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        response.ShouldNotBeNull();
        response.AccessToken.ShouldBe("at-only");
        response.IdToken.ShouldBe(string.Empty);
        response.TokenType.ShouldBe("Bearer");
        response.ExpiresIn.ShouldBe(60);
        response.RefreshToken.ShouldBeNull();
    }

    /// <summary>Given a valid OIDC config, when ExchangeCodeAsync
    /// dispatches, then the request URL targets
    /// <c>{authority}/protocol/openid-connect/token</c>.</summary>
    [Fact(DisplayName = "Given a valid OIDC config, when ExchangeCodeAsync dispatches, then the request URL targets {authority}/protocol/openid-connect/token")]
    public async Task ExchangeCodeAsync_PostsToKeycloakTokenEndpointAsync()
    {
        var fixture = BuildClient();
        fixture.ConfigReader.GetForOrgAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildOidcConfig(fixture.Protector));
        fixture.Handler.SetResponse(static _ => ScriptedResponse.Json(HttpStatusCode.OK, TokenResponseJson));

        await fixture.Client.ExchangeCodeAsync(
            Guid.NewGuid(),
            TestAuthorizationCode,
            TestCodeVerifier,
            TestRedirectUri,
            CancellationToken.None);

        fixture.Handler.LastRequest.ShouldNotBeNull();
        fixture.Handler.LastRequest.RequestUri!.ToString().ShouldBe(TokenEndpointUrl);
        fixture.Handler.LastRequest.Method.ShouldBe(HttpMethod.Post);
    }

    /// <summary>
    ///     Bundle everything the tests need: client + handler +
    ///     reader + a working <see cref="OrgAuthProviderSecretProtector" />.
    ///     Single helper so every test sees a fresh handler and a
    ///     fresh scriptable HttpClient.
    /// </summary>
    private static ClientFixture BuildClient()
    {
        var handler = new ScriptedHttpHandler();
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Plexor-OidcDiscovery")
            .Returns(_ => new HttpClient(handler));
        var configReader = Substitute.For<IOrgAuthProviderConfigReader>();
        var protector = new OrgAuthProviderSecretProtector(new EphemeralDataProtectionProvider());
        var client = new OidcTokenClient(
            factory,
            configReader,
            protector,
            NullLogger<OidcTokenClient>.Instance);
        return new ClientFixture(client, handler, configReader, protector);
    }

    private static OrgAuthProviderConfig BuildOidcConfig(
        OrgAuthProviderSecretProtector protector,
        string plaintextSecret = TestClientSecret)
    {
        return new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            Provider = OrgAuthProvider.Oidc,
            OidcAuthority = TestAuthority,
            OidcClientId = TestClientId,
            OidcClientSecretProtected = plaintextSecret.Length == 0
                ? string.Empty
                : protector.Encrypt(plaintextSecret),
            OidcScopes = ["openid", "profile", "email"],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    ///     Tuple-style fixture bundle for the tests — keeps the
    ///     build helper single and side-effect free.
    /// </summary>
    /// <param name="Client"></param>
    /// <param name="Handler"></param>
    /// <param name="ConfigReader"></param>
    /// <param name="Protector"></param>
    private sealed record ClientFixture(
        OidcTokenClient Client,
        ScriptedHttpHandler Handler,
        IOrgAuthProviderConfigReader ConfigReader,
        OrgAuthProviderSecretProtector Protector);

    /// <summary>
    ///     Ephemeral in-memory data-protection provider — no keyring
    ///     on disk. Each instance gets its own (separate from any
    ///     other test) so ciphertext does not bleed.
    /// </summary>
    private sealed class EphemeralDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose)
        {
            return new EphemeralDataProtector(purpose);
        }

        private sealed class EphemeralDataProtector(string purpose) : IDataProtector
        {
            private readonly string purpose = purpose;

            public byte[] Protect(byte[] plaintext)
            {
                var prefix = Encoding.UTF8.GetBytes($"epd:{purpose}:");
                var output = new byte[prefix.Length + plaintext.Length];
                Buffer.BlockCopy(prefix, 0, output, 0, prefix.Length);
                Buffer.BlockCopy(plaintext, 0, output, prefix.Length, plaintext.Length);
                return output;
            }

            public byte[] Unprotect(byte[] protectedData)
            {
                var prefix = Encoding.UTF8.GetBytes($"epd:{purpose}:");
                if (protectedData.Length < prefix.Length
                    || !protectedData.Take(prefix.Length).SequenceEqual(prefix))
                {
                    throw new System.Security.Cryptography.CryptographicException(
                        "EphemeralDataProtector: bad purpose or malformed ciphertext.");
                }

                var output = new byte[protectedData.Length - prefix.Length];
                Buffer.BlockCopy(protectedData, prefix.Length, output, 0, output.Length);
                return output;
            }

            /// <summary>
            /// IDataProtector inherits CreateProtector from
            /// IDataProtectionProvider — a sub-purpose creates a
            /// child protector that prefixes its purpose into the
            /// ciphertext header.
            /// </summary>
            /// <param name="subPurpose"></param>
            /// <returns></returns>
            public IDataProtector CreateProtector(string subPurpose)
            {
                return new EphemeralDataProtector($"{purpose}:{subPurpose}");
            }
        }
    }

    /// <summary>
    ///     Scripted HttpMessageHandler — returns the configured
    ///     response and remembers the last outbound request for
    ///     assertions. Captures everything in a single class so the
    ///     test doesn't need a private field pair.
    /// </summary>
    private sealed class ScriptedHttpHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, ScriptedResponse>? responseFactory;

        public int CallCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        public void SetResponse(Func<HttpRequestMessage, ScriptedResponse> factory)
        {
            responseFactory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            if (responseFactory is null)
            {
                throw new InvalidOperationException(
                    "ScriptedHttpHandler: SetResponse was not called before SendAsync.");
            }

            var response = responseFactory(request).ToHttpResponse();
            return Task.FromResult(response);
        }
    }

    /// <summary>
    ///     Tiny HTTP response builder paired with
    ///     <see cref="ScriptedHttpHandler" />.
    /// </summary>
    private readonly struct ScriptedResponse
    {
        private readonly HttpStatusCode statusCode;
        private readonly string? body;

        private ScriptedResponse(HttpStatusCode statusCode, string? body)
        {
            this.statusCode = statusCode;
            this.body = body;
        }

        public static ScriptedResponse Json(HttpStatusCode statusCode, string json)
        {
            return new ScriptedResponse(statusCode, json);
        }

        public static ScriptedResponse Status(HttpStatusCode statusCode)
        {
            return new ScriptedResponse(statusCode, null);
        }

        public HttpResponseMessage ToHttpResponse()
        {
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
            {
                response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return response;
        }
    }
}
