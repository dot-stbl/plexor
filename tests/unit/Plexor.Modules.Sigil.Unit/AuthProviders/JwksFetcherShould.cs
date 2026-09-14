// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JwksFetcherShould — exercise the JWKS fetch + cache path. Uses a
// custom HttpMessageHandler stub instead of MockHttp (not a test
// dep yet) so the fetcher sees realistic HTTP responses without
// touching the network. Covers the three branches: cold cache +
// warm cache + transport failure.
// ============================================================================

using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="Plexor.Modules.Sigil.Infrastructure.AuthProviders.JwksFetcher" />.
/// </summary>
public sealed class JwksFetcherShould
{
    private const string TestAuthority = "https://kc.example.com/realms/plexor";
    private const string EmptyJwksJson = /*lang=json,strict*/ "{\"keys\":[]}";

    private const string DiscoveryJson = /*lang=json,strict*/ "{\"issuer\":\"https://kc.example.com/realms/plexor\",\"jwks_uri\":\"https://kc.example.com/realms/plexor/protocol/openid-connect/certs\"}";

    private const string DiscoveryMissingJwksUriJson = /*lang=json,strict*/ "{\"issuer\":\"https://kc.example.com/realms/plexor\"}";

    /// <summary>Given an authority, when GetKeySetAsync is called for
    /// the first time, then fetches the discovery + JWKS via HTTP.</summary>
    [Fact(DisplayName = "Given an authority, when GetKeySetAsync is called cold, then fetches discovery + JWKS")]
    public async Task GetKeySetAsync_FirstCall_FetchesFromHttpAsync()
    {
        var handler = new ScriptedHttpHandler(
            static _ => ScriptedResponse.Json(HttpStatusCode.OK, DiscoveryJson),
            static _ => ScriptedResponse.Json(HttpStatusCode.OK, EmptyJwksJson));
        var fetcher = BuildFetcher(handler);

        var keySet = await fetcher.GetKeySetAsync(TestAuthority, CancellationToken.None);

        keySet.ShouldNotBeNull();
        keySet.Keys.Count.ShouldBe(0);
        handler.CallCount.ShouldBe(2);
    }

    /// <summary>Given an authority whose JWKS is already in the cache,
    /// when GetKeySetAsync is called again, then returns the cached
    /// set and does NOT issue another HTTP request.</summary>
    [Fact(DisplayName = "Given a warm cache, when GetKeySetAsync runs again, then the cached set is returned without an HTTP call")]
    public async Task GetKeySetAsync_SecondCall_ReturnsCachedAsync()
    {
        var handler = new ScriptedHttpHandler(
            static _ => ScriptedResponse.Json(HttpStatusCode.OK, DiscoveryJson),
            static _ => ScriptedResponse.Json(HttpStatusCode.OK, EmptyJwksJson));
        var fetcher = BuildFetcher(handler);

        var first = await fetcher.GetKeySetAsync(TestAuthority, CancellationToken.None);
        var second = await fetcher.GetKeySetAsync(TestAuthority, CancellationToken.None);

        first.Keys.Count.ShouldBe(0);
        second.Keys.Count.ShouldBe(0);
        handler.CallCount.ShouldBe(2);
    }

    /// <summary>Given a trailing-slash on the authority, when
    /// GetKeySetAsync is called, then both calls hit the same cache
    /// entry (normalisation collapses the slash).</summary>
    [Fact(DisplayName = "Given an authority with a trailing slash, when GetKeySetAsync is called, then both shapes resolve to the same cache entry")]
    public async Task GetKeySetAsync_TrailingSlash_NormalisesCacheKeyAsync()
    {
        var handler = new ScriptedHttpHandler(
            static _ => ScriptedResponse.Json(HttpStatusCode.OK, DiscoveryJson),
            static _ => ScriptedResponse.Json(HttpStatusCode.OK, EmptyJwksJson));
        var fetcher = BuildFetcher(handler);

        await fetcher.GetKeySetAsync(TestAuthority, CancellationToken.None);
        await fetcher.GetKeySetAsync(TestAuthority + "/", CancellationToken.None);

        handler.CallCount.ShouldBe(2);
    }

    /// <summary>Given an unreachable authority, when GetKeySetAsync
    /// is called, then propagates the HTTP transport failure.</summary>
    [Fact(DisplayName = "Given an unreachable authority, when GetKeySetAsync runs, then throws HttpRequestException")]
    public async Task GetKeySetAsync_WithUnreachableAuthority_ThrowsAsync()
    {
        var handler = new ScriptedHttpHandler(_ => ScriptedResponse.Status(HttpStatusCode.ServiceUnavailable));
        var fetcher = BuildFetcher(handler);

        await Should.ThrowAsync<HttpRequestException>(async () =>
            await fetcher.GetKeySetAsync(TestAuthority, CancellationToken.None));
    }

    /// <summary>Given a discovery document missing the jwks_uri field,
    /// when GetKeySetAsync runs, then throws InvalidOperationException.</summary>
    [Fact(DisplayName = "Given a discovery document missing jwks_uri, when GetKeySetAsync runs, then throws")]
    public async Task GetKeySetAsync_WithDiscoveryMissingJwksUri_ThrowsAsync()
    {
        var handler = new ScriptedHttpHandler(_ => ScriptedResponse.Json(HttpStatusCode.OK, DiscoveryMissingJwksUriJson));
        var fetcher = BuildFetcher(handler);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await fetcher.GetKeySetAsync(TestAuthority, CancellationToken.None));
    }

    private static JwksFetcher BuildFetcher(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Plexor-OidcDiscovery").Returns(_ => new HttpClient(handler));
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new JwksFetcher(factory, cache, NullLogger<JwksFetcher>.Instance);
    }

    /// <summary>
    ///     Minimal scripted HttpMessageHandler — returns the next
    ///     response in <see cref="responses" /> for each call, in
    ///     order. The constructor variants cover the two-response
    ///     shape (discovery + JWKS) and the single-response shape
    ///     (failure / missing field).
    /// </summary>
    private sealed class ScriptedHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, ScriptedResponse>[] responses;
        private int index;

        public ScriptedHttpHandler(params Func<HttpRequestMessage, ScriptedResponse>[] responses)
        {
            if (responses.Length == 0)
            {
                throw new ArgumentException("At least one response is required.", nameof(responses));
            }

            this.responses = responses;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            // Cycle through responses — for the cold-cache path,
            // the first request matches the discovery response and
            // the second matches the JWKS response; for warm-cache
            // and failure paths the handler stops issuing requests
            // after the first one anyway.
            var response = responses[Math.Min(index, responses.Length - 1)](request);
            index++;
            return Task.FromResult(response.ToHttpResponse());
        }
    }

    /// <summary>
    ///     A simple HTTP response builder — pair with
    ///     <see cref="ScriptedHttpHandler" /> to assemble test fixtures.
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
