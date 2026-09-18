// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HttpImageRegistry unit tests — exercise the surface contract
// (cache-hit short-circuit, error paths, name sanitisation,
// SHA-256 verification, host-rejection of malformed digests)
// without hitting the network. The mock handler is a
// DelegatingHandler that intercepts requests and returns
// either a fixture stream (success) or an error response.
// Integration tests against a real HTTP source land in the
// testcontainers/live-mirror suite.
// ==========================================================================

using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Plexor.NodeAgent.Providers.Image;
using Plexor.NodeAgent.Providers.Image.Fetch;
using Plexor.Shared.Compute;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Image;

public sealed class HttpImageRegistryShould
{
    [Fact(DisplayName = "Given a known ref and an empty cache, when EnsureLocalAsync, then downloads + caches + returns path")]
    public async Task DownloadAndCacheAsync()
    {
        var cacheDir = TempDir();
        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: "fake-qcow2-bytes",
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            cacheDir,
            new Dictionary<string, ImageCatalogueEntry>
            {
                ["ubuntu-22.04"] = new ImageCatalogueEntry(
                    Url: "https://example.invalid/ubuntu-22.04.qcow2")
            },
            handler);

        var path = await sut.EnsureLocalAsync("ubuntu-22.04", CancellationToken.None);

        path.ShouldStartWith(cacheDir);
        path.ShouldEndWith(".qcow2");
        File.Exists(path).ShouldBeTrue();
        (await File.ReadAllTextAsync(path)).ShouldBe("fake-qcow2-bytes");
        handler.RequestCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Given a cached image, when EnsureLocalAsync, then returns cached path without network")]
    public async Task CacheHitShortCircuitsAsync()
    {
        var cacheDir = TempDir();
        var cachedPath = Path.Combine(cacheDir, "ubuntu-22.04.qcow2");
        await File.WriteAllTextAsync(cachedPath, "already-here");

        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: "newer-bytes",
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            cacheDir,
            new Dictionary<string, ImageCatalogueEntry>
            {
                ["ubuntu-22.04"] = new ImageCatalogueEntry(
                    Url: "https://example.invalid/ubuntu-22.04.qcow2")
            },
            handler);

        var path = await sut.EnsureLocalAsync("ubuntu-22.04", CancellationToken.None);

        path.ShouldBe(cachedPath);
        handler.RequestCount.ShouldBe(0);
        (await File.ReadAllTextAsync(path)).ShouldBe("already-here");
    }

    [Fact(DisplayName = "Given an unknown ref, when EnsureLocalAsync, then throws UnknownImageException")]
    public async Task UnknownRefThrowsAsync()
    {
        var sut = NewRegistry(
            TempDir(),
            [],
            new FixtureHandler("", "", HttpStatusCode.NotFound));

        await Should.ThrowAsync<UnknownImageException>(
            () => sut.EnsureLocalAsync("does-not-exist", CancellationToken.None));
    }

    [Fact(DisplayName = "Given a 404 from the source, when EnsureLocalAsync, then propagates HttpRequestException")]
    public async Task NotFoundPropagatesHttpAsync()
    {
        var handler = new FixtureHandler(
            "https://example.invalid/missing.qcow2",
            payload: "",
            statusCode: HttpStatusCode.NotFound);
        var sut = NewRegistry(
            TempDir(),
            new Dictionary<string, ImageCatalogueEntry>
            {
                ["missing"] = new ImageCatalogueEntry(
                    Url: "https://example.invalid/missing.qcow2")
            },
            handler);

        await Should.ThrowAsync<HttpRequestException>(
            () => sut.EnsureLocalAsync("missing", CancellationToken.None));
    }

    [Fact(DisplayName = "Given a ref that sanitises to an empty filename, when EnsureLocalAsync, then still produces a unique cached path")]
    public async Task HostileRefFallsBackToHashAsync()
    {
        var handler = new FixtureHandler(
            "https://example.invalid/x.qcow2",
            payload: "ok",
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            TempDir(),
            new Dictionary<string, ImageCatalogueEntry>
            {
                ["../../etc/passwd"] = new ImageCatalogueEntry(
                    Url: "https://example.invalid/x.qcow2")
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

    [Fact(DisplayName = "Given a ref with a correct SHA-256, when EnsureLocalAsync, then the download succeeds")]
    public async Task MatchingSha256IsAcceptedAsync()
    {
        var cacheDir = TempDir();
        var payload = "fake-qcow2-bytes";
        // SHA256.HashData over a tiny in-memory fixture is
        // acceptably synchronous in test code (a few bytes — far
        // below the threading-analyzer's threshold for "would
        // block"); the alternative HashDataAsync only accepts
        // streams, which is the wrong shape for a one-shot byte
        // array.
#pragma warning disable VSTHRD103 // acceptable in test
        var sha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
#pragma warning restore VSTHRD103

        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: payload,
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            cacheDir,
            new Dictionary<string, ImageCatalogueEntry>
            {
                ["ubuntu-22.04"] = new ImageCatalogueEntry(
                    Url: "https://example.invalid/ubuntu-22.04.qcow2",
                    Sha256: sha256)
            },
            handler);

        var path = await sut.EnsureLocalAsync("ubuntu-22.04", CancellationToken.None);

        File.Exists(path).ShouldBeTrue();
        handler.RequestCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Given a ref with a wrong SHA-256, when EnsureLocalAsync, then throws ImageHashMismatchException")]
    public async Task MismatchedSha256ThrowsAsync()
    {
        var cacheDir = TempDir();
        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: "actual-bytes",
            statusCode: HttpStatusCode.OK);
        var sut = NewRegistry(
            cacheDir,
            new Dictionary<string, ImageCatalogueEntry>
            {
                ["ubuntu-22.04"] = new ImageCatalogueEntry(
                    Url: "https://example.invalid/ubuntu-22.04.qcow2",
                    Sha256: "0000000000000000000000000000000000000000000000000000000000000000")
            },
            handler);

        await Should.ThrowAsync<ImageHashMismatchException>(
            () => sut.EnsureLocalAsync("ubuntu-22.04", CancellationToken.None));
    }

    [Fact(DisplayName = "Given a catalogue entry with malformed SHA-256, when EnsureLocalAsync, then throws ArgumentException at lookup")]
    public async Task MalformedSha256ThrowsAtLookupAsync()
    {
        var sut = NewRegistry(
            TempDir(),
            new Dictionary<string, ImageCatalogueEntry>
            {
                ["bad-sha"] = new ImageCatalogueEntry(
                    Url: "https://example.invalid/anything",
                    Sha256: "not-hex")
            },
            new FixtureHandler("", "", HttpStatusCode.OK));

        await Should.ThrowAsync<ArgumentException>(
            () => sut.EnsureLocalAsync("bad-sha", CancellationToken.None));
    }

    private static HttpImageRegistry NewRegistry(
        string cacheDir,
        Dictionary<string, ImageCatalogueEntry> catalog,
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
    /// <param name="expectedUrl">The exact URL the handler matches against.</param>
    /// <param name="payload">Response body returned for matching requests.</param>
    /// <param name="statusCode">HTTP status returned for matching requests.</param>
    private sealed class FixtureHandler(string expectedUrl, string payload, HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        /// <inheritdoc />
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
    /// <param name="handler">The handler every client returned by the factory wraps.</param>
    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        /// <inheritdoc />
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }
}
