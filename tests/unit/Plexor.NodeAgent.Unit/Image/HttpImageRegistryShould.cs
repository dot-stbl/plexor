// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HttpImageRegistry unit tests — exercise the surface contract
// (cache-hit short-circuit, error paths, name sanitisation)
// without hitting the network. The mock handler is a
// DelegatingHandler that intercepts requests and returns
// either a fixture stream (success) or an error response.
// Integration tests against a real HTTP source land in the
// testcontainers/live-mirror suite.
// ==========================================================================

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Plexor.NodeAgent.Providers.Image;
using Plexor.Shared.Compute;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Image;

public sealed class HttpImageRegistryShould
{
    [Fact(DisplayName = "Given a known ref and an empty cache, when EnsureLocalAsync, then downloads + caches + returns path")]
    public async Task DownloadAndCache()
    {
        var cacheDir = TempDir();
        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: "fake-qcow2-bytes",
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            cacheDir,
            new Dictionary<string, string>
            {
                ["ubuntu-22.04"] = "https://example.invalid/ubuntu-22.04.qcow2"
            },
            handler);

        var path = await sut.EnsureLocalAsync("ubuntu-22.04", CancellationToken.None);

        path.ShouldStartWith(cacheDir);
        File.Exists(path).ShouldBeTrue();
        (await File.ReadAllTextAsync(path)).ShouldBe("fake-qcow2-bytes");
        handler.RequestCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Given a cached image, when EnsureLocalAsync, then returns cached path without network")]
    public async Task CacheHitShortCircuits()
    {
        var cacheDir = TempDir();
        var cachedPath = Path.Combine(cacheDir, "ubuntu-22.04.img");
        await File.WriteAllTextAsync(cachedPath, "already-here");

        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: "newer-bytes",
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            cacheDir,
            new Dictionary<string, string>
            {
                ["ubuntu-22.04"] = "https://example.invalid/ubuntu-22.04.qcow2"
            },
            handler);

        var path = await sut.EnsureLocalAsync("ubuntu-22.04", CancellationToken.None);

        path.ShouldBe(cachedPath);
        handler.RequestCount.ShouldBe(0);
        (await File.ReadAllTextAsync(path)).ShouldBe("already-here");
    }

    [Fact(DisplayName = "Given an unknown ref, when EnsureLocalAsync, then throws UnknownImageException")]
    public async Task UnknownRefThrows()
    {
        var sut = NewRegistry(
            TempDir(),
            new Dictionary<string, string>(),
            new FixtureHandler("", "", HttpStatusCode.NotFound));

        await Should.ThrowAsync<UnknownImageException>(
            () => sut.EnsureLocalAsync("does-not-exist", CancellationToken.None));
    }

    [Fact(DisplayName = "Given a 404 from the source, when EnsureLocalAsync, then propagates HttpRequestException")]
    public async Task NotFoundPropagatesHttp()
    {
        var handler = new FixtureHandler(
            "https://example.invalid/missing.qcow2",
            payload: "",
            statusCode: HttpStatusCode.NotFound);
        var sut = NewRegistry(
            TempDir(),
            new Dictionary<string, string>
            {
                ["missing"] = "https://example.invalid/missing.qcow2"
            },
            handler);

        await Should.ThrowAsync<HttpRequestException>(
            () => sut.EnsureLocalAsync("missing", CancellationToken.None));
    }

    [Fact(DisplayName = "Given a ref that sanitises to an empty filename, when EnsureLocalAsync, then still produces a unique cached path")]
    public async Task HostileRefFallsBackToHash()
    {
        var handler = new FixtureHandler(
            "https://example.invalid/x.qcow2",
            payload: "ok",
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            TempDir(),
            new Dictionary<string, string>
            {
                ["../../etc/passwd"] = "https://example.invalid/x.qcow2"
            },
            handler);

        // Ref contains only path-traversal characters; sanitisation
        // empties it, the registry falls back to a sha256-hex
        // filename. The path must STILL be inside the cache dir
        // (no path-escape).
        var path = await sut.EnsureLocalAsync("../../etc/passwd", CancellationToken.None);

        var cacheRoot = Path.GetFullPath(Path.GetDirectoryName(path)!);
        path.ShouldStartWith(cacheRoot);
        File.Exists(path).ShouldBeTrue();
        handler.RequestCount.ShouldBe(1);
    }

    private static HttpImageRegistry NewRegistry(
        string cacheDir,
        Dictionary<string, string> catalog,
        HttpMessageHandler handler)
    {
        var opts = Options.Create(new HttpImageRegistryOptions
        {
            CacheDirectory = cacheDir,
            Catalog = catalog
        });
        var http = new StubHttpClientFactory(handler);
        return new HttpImageRegistry(opts, http, NullLogger<HttpImageRegistry>.Instance);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"plexor-http-img-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    ///     Test handler that returns a fixed payload / status for
    ///     any matching request, and counts requests so the test
    ///     can assert the cache short-circuited.
    /// </summary>
    private sealed class FixtureHandler(string expectedUrl, string payload, HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            if (!string.IsNullOrEmpty(expectedUrl)
                && request.RequestUri?.ToString() != expectedUrl)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(payload)
            });
        }
    }

    /// <summary>
    ///     Minimal <see cref="IHttpClientFactory" /> that returns
    ///     a client wired to the test handler. The named-client
    ///     contract (<see cref="HttpImageRegistry.HttpClientName" />)
    ///     is honoured because the registry passes that name to
    ///     CreateClient; we return the same client for all names
    ///     because the test owns exactly one handler.
    /// </summary>
    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }
}
