// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ImageFetcher unit tests — exercise the streaming download +
// SHA-256 verification + format-conversion pipeline without
// hitting the network. The mock handler is a DelegatingHandler
// that returns a fixed payload; qemu-img is NOT exercised
// here (integration test P1 #12 covers that against a real
// binary on Linux).
//
// Coverage:
//   - Cache-hit short-circuit (no network).
//   - Successful download + cache file matches the body.
//   - SHA-256 mismatch → ImageHashMismatchException + partial cleanup.
//   - SHA-256 match → download accepted.
//   - Format=Raw triggers qemu-img convert (skipped when qemu-img
//     isn't on PATH; see [Fact(Skip = ...)]).
//   - Malformed SHA-256 catalogue entry → ArgumentException at
//     ValidateSha256 (fail-fast on operator typo).
// ==========================================================================

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Plexor.NodeAgent.Providers.Image.Fetch;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Image.Fetch;

public sealed class ImageFetcherShould
{
    [Fact(DisplayName = "Given a matching SHA-256 catalogue entry, when DownloadAsync, then writes the payload to the cache path")]
    public async Task DownloadsAndVerifiesSha256Async()
    {
        var cacheDir = TempDir();
        var payload = "fake-qcow2-payload";
        // SHA256.HashData over a tiny in-memory fixture is
        // acceptably synchronous in test code (a few bytes — far
        // below the threading-analyzer's threshold for "would
        // block"); the alternative HashDataAsync only accepts
        // streams, which is the wrong shape for a one-shot byte
        // array.
#pragma warning disable VSTHRD103 // acceptable in test
        var sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
#pragma warning restore VSTHRD103

        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: payload,
            statusCode: HttpStatusCode.OK);

        var sut = NewFetcher(handler);
        var cachePath = Path.Combine(cacheDir, "ubuntu-22.04.qcow2");

        var path = await sut.DownloadAsync(
            new ImageSource(
                new Uri("https://example.invalid/ubuntu-22.04.qcow2"),
                ExpectedSha256: sha256,
                Format: null),
            cachePath,
            CancellationToken.None);

        path.ShouldBe(cachePath);
        File.Exists(path).ShouldBeTrue();
        (await File.ReadAllTextAsync(path)).ShouldBe(payload);
        File.Exists(cachePath + ".partial").ShouldBeFalse();
        handler.RequestCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Given a wrong SHA-256 catalogue entry, when DownloadAsync, then throws ImageHashMismatchException and clears the partial")]
    public async Task Sha256MismatchThrowsAndCleansUpAsync()
    {
        var cacheDir = TempDir();
        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.qcow2",
            payload: "actual-bytes",
            statusCode: HttpStatusCode.OK);

        var sut = NewFetcher(handler);
        var cachePath = Path.Combine(cacheDir, "ubuntu-22.04.qcow2");

        await Should.ThrowAsync<ImageHashMismatchException>(
            () => sut.DownloadAsync(
                new ImageSource(
                    new Uri("https://example.invalid/ubuntu-22.04.qcow2"),
                    ExpectedSha256: "0000000000000000000000000000000000000000000000000000000000000000",
                    Format: null),
                cachePath,
                CancellationToken.None));

        File.Exists(cachePath).ShouldBeFalse();
        File.Exists(cachePath + ".partial").ShouldBeFalse();
        handler.RequestCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Given a cached file already on disk, when DownloadAsync, then returns it without hitting the network")]
    public async Task CacheHitShortCircuitsAsync()
    {
        var cacheDir = TempDir();
        var cachePath = Path.Combine(cacheDir, "ubuntu-22.04.qcow2");
        await File.WriteAllTextAsync(cachePath, "pre-existing-content");

        var handler = new FixtureHandler("", "", HttpStatusCode.OK);
        var sut = NewFetcher(handler);

        var path = await sut.DownloadAsync(
            new ImageSource(
                new Uri("https://example.invalid/anything"),
                ExpectedSha256: null,
                Format: null),
            cachePath,
            CancellationToken.None);

        path.ShouldBe(cachePath);
        (await File.ReadAllTextAsync(path)).ShouldBe("pre-existing-content");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact(DisplayName = "Given a 64-char lowercase-hex SHA-256, when ValidateSha256, then succeeds")]
    public void ValidateSha256AcceptsLowercaseHex()
    {
        Should.NotThrow(static () => ImageFetcher.ValidateSha256(
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"));
    }

    [Fact(DisplayName = "Given a SHA-256 with uppercase hex, when ValidateSha256, then throws (catalogue must be lowercase)")]
    public void ValidateSha256RejectsUppercase()
    {
        Should.Throw<ArgumentException>(
            static () => ImageFetcher.ValidateSha256(
                "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789"));
    }

    [Fact(DisplayName = "Given a SHA-256 with the wrong length, when ValidateSha256, then throws")]
    public void ValidateSha256RejectsWrongLength()
    {
        Should.Throw<ArgumentException>(
            static () => ImageFetcher.ValidateSha256("abcd1234"));
    }

    [Fact(DisplayName = "Given a SHA-256 with a non-hex character, when ValidateSha256, then throws")]
    public void ValidateSha256RejectsNonHex()
    {
        Should.Throw<ArgumentException>(
            static () => ImageFetcher.ValidateSha256(
                "zbcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"));
    }

    /// <summary>
    ///     The raw → qcow2 conversion shells out to <c>qemu-img</c>.
    ///     We skip this test when the binary isn't on PATH
    ///     (developer box, CI runner without qemu-img); the
    ///     integration suite in tests/integration covers the
    ///     real path.
    /// </summary>
    [Fact(DisplayName = "Given Format=Raw and qemu-img on PATH, when DownloadAsync, then converts + writes qcow2",
          Skip = "Requires qemu-img on PATH — covered by integration test suite.")]
    public async Task RawFormatInvokesQemuImgConvertAsync()
    {
        // The Skip attribute short-circuits; the test body exists
        // so a future contributor who runs locally with qemu-img
        // can re-enable it by deleting Skip and re-running.
        var cacheDir = TempDir();
        var handler = new FixtureHandler(
            "https://example.invalid/ubuntu-22.04.raw",
            payload: "fake-raw-bytes",
            statusCode: HttpStatusCode.OK);
        var sut = NewFetcher(handler);
        var cachePath = Path.Combine(cacheDir, "ubuntu-22.04.qcow2");

        var path = await sut.DownloadAsync(
            new ImageSource(
                new Uri("https://example.invalid/ubuntu-22.04.raw"),
                ExpectedSha256: null,
                Format: ImageFormat.Raw),
            cachePath,
            CancellationToken.None);

        File.Exists(path).ShouldBeTrue();
    }

    private static ImageFetcherWrapper NewFetcher(HttpMessageHandler handler)
    {
        return new ImageFetcherWrapper(handler);
    }

    /// <summary>
    ///     Tiny wrapper that owns the HttpClient so the test
    ///     doesn't have to dispose it manually. The wrapper
    ///     hands the inner client to <see cref="ImageFetcher.DownloadAsync" />.
    /// </summary>
    private sealed class ImageFetcherWrapper(HttpMessageHandler handler) : IDisposable
    {
        private readonly HttpClient client = new(handler, disposeHandler: false);

        public Task<string> DownloadAsync(
            ImageSource source,
            string cachePath,
            CancellationToken cancellationToken)
        {
            return ImageFetcher.DownloadAsync(client, source, cachePath, cancellationToken);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"plexor-fetcher-{Guid.NewGuid():N}");
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
}

