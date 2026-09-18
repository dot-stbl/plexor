// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ImageFetcher — file-static helper for streaming HTTP/HTTPS image
// download + SHA256 verification + format conversion (raw → qcow2).
// Lives in its own file per class-decomposition (helpers without
// DI → file-static class, not private method on the registry).
//
// Pipeline:
//
//   1. HTTP GET with HttpCompletionOption.ResponseHeadersRead
//      (don't buffer the body — read it as it arrives).
//   2. Stream the response body into a `.partial` file alongside
//      the cache path. The file is moved into place only after
//      the hash check (and conversion, if any) succeed.
//   3. If the catalogue entry supplied an `ExpectedSha256`,
//      SHA256-hash the downloaded file and reject mismatches.
//      Mismatches delete the partial file so the next attempt
//      re-downloads cleanly (no stale-partial blocking).
//   4. If the catalogue entry supplied `Format != Qcow2`,
//      convert to qcow2 in place via `qemu-img convert -O qcow2`.
//      The converted file replaces the raw download.
//   5. Move the final file to the canonical cache path. Idempotent
//      — if the cache file already exists, the call returns it
//      without re-downloading.
//
// Why file-static and not on HttpImageRegistry: HttpImageRegistry
// is an `IImageRegistry` impl with DI dependencies (IOptions,
// IHttpClientFactory, ILogger); ImageFetcher is pure I/O + a
// process spawn and is tested directly with a stub HttpClient.
// ==========================================================================

using System.Globalization;
using System.Security.Cryptography;
using Plexor.NodeAgent.Providers.Storage;

namespace Plexor.NodeAgent.Providers.Image.Fetch;

/// <summary>
///     Catalogue entry for an image source. Passed by
///     <c>HttpImageRegistry</c> on every <c>EnsureLocalAsync</c>
///     call. <see cref="Url" /> is mandatory; <see cref="ExpectedSha256" />
///     and <see cref="Format" /> are optional (when missing, the
///     fetcher trusts the URL and produces qcow2 by default).
/// </summary>
/// <param name="Url">
///     Absolute http/https URL the body is streamed from.
/// </param>
/// <param name="ExpectedSha256">
///     Lowercase hex SHA-256 of the *downloaded* (raw) bytes
///     (i.e. before format conversion). When non-null, the
///     fetcher hashes the partial file and rejects the download
///     if the digest doesn't match.
/// </param>
/// <param name="Format">
///     Format of the downloaded bytes. When null, the fetcher
///     assumes qcow2 (skipping the conversion step). When
///     <see cref="ImageFormat.Raw" />, the fetcher runs
///     <c>qemu-img convert -O qcow2</c> on the partial file
///     before moving it into the cache.
/// </param>
public sealed record ImageSource(
    Uri Url,
    string? ExpectedSha256,
    ImageFormat? Format);

/// <summary>
///     Pure I/O helper that downloads an image to the local
///     cache directory, verifies its SHA-256, and converts to
///     qcow2 if needed. Stateless — each call is independent.
/// </summary>
public static class ImageFetcher
{
    /// <summary>
    ///     Download + verify + (optional) convert. Idempotent: if
    ///     <paramref name="cachePath" /> already exists, returns
    ///     it without re-downloading.
    /// </summary>
    /// <param name="http">
    ///     HttpClient to use (caller-owned; the fetcher never
    ///     disposes it). The named-client contract on
    ///     <c>HttpImageRegistry</c> is honoured by passing a
    ///     client pre-configured with a long timeout + redirects.
    /// </param>
    /// <param name="source">Catalogue entry with URL + optional SHA256 + format.</param>
    /// <param name="cachePath">
    ///     Absolute path to the final cached image. The directory
    ///     is created if missing. The fetcher writes the
    ///     download to <c>{cachePath}.partial</c> first, then
    ///     atomically renames into place.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns>The cache path (echoes <paramref name="cachePath" />).</returns>
    /// <exception cref="ImageHashMismatchException">
    ///     When <see cref="ImageSource.ExpectedSha256" /> is set
    ///     and the downloaded bytes don't match.
    /// </exception>
    /// <exception cref="ImageConversionException">
    ///     When <see cref="ImageSource.Format" /> is
    ///     <see cref="ImageFormat.Raw" /> and <c>qemu-img convert</c>
    ///     exits non-zero.
    /// </exception>
    /// <exception cref="HttpRequestException">
    ///     Propagated from the HttpClient on 4xx/5xx. The
    ///     partial file is deleted before rethrow.
    /// </exception>
    public static async Task<string> DownloadAsync(
        HttpClient http,
        ImageSource source,
        string cachePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);

        if (File.Exists(cachePath))
        {
            // Trust the cache. A future revision can verify the
            // cached file's hash against a manifest; for v0.1 the
            // operator-managed URL is the trust root.
            return cachePath;
        }

        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var partialPath = cachePath + ".partial";

        try
        {
            using var response = await http.GetAsync(
                source.Url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = File.Create(partialPath))
            {
                await sourceStream.CopyToAsync(destination, cancellationToken);
            }

            // SHA-256 verification. We hash the on-disk file rather
            // than the in-memory stream so we don't have to
            // re-buffer a 2GB cloud image into RAM just to hash it.
            if (!string.IsNullOrWhiteSpace(source.ExpectedSha256))
            {
                var actual = await ComputeSha256HexAsync(partialPath, cancellationToken);
                if (!string.Equals(actual, source.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ImageHashMismatchException(source.ExpectedSha256, actual);
                }
            }

            // Format conversion. Only raw → qcow2 is supported in
            // v0.1; future image formats (vmdk, vdi) get their
            // own switches here. The convert is a separate step
            // so the SHA-256 we just verified is over the raw
            // bytes (not the converted qcow2), and a failed
            // convert leaves the partial file available for
            // inspection.
            if (source.Format == ImageFormat.Raw)
            {
                var convertedPath = partialPath + ".qcow2";
                await QemuImageRunner.RunAsync(
                    $"convert -f raw -O qcow2 \"{partialPath}\" \"{convertedPath}\"",
                    cancellationToken);

                // qcow2 conversion replaces the partial; remove
                // the raw before renaming the converted into
                // place so the final cachePath rename target
                // exists.
                File.Delete(partialPath);
                File.Move(convertedPath, partialPath);
            }

            File.Move(partialPath, cachePath, overwrite: false);
        }
        catch
        {
            // Best-effort cleanup on partial-file scenarios. We
            // don't want to leave a stale .partial that blocks
            // the next download — the file-exists short-circuit
            // would skip re-download.
            try
            {
                File.Delete(partialPath);
            }
            catch (FileNotFoundException)
            {
                // Partial never landed — fine.
            }
            catch (DirectoryNotFoundException)
            {
                // Cache directory vanished mid-flight (operator
                // unmounted a tmpfs?). Can't do anything useful.
            }

            throw;
        }

        return cachePath;
    }

    /// <summary>
    ///     Compute the lowercase hex SHA-256 of a file's bytes,
    ///     streaming (the file isn't loaded into memory). Used
    ///     after the partial-file download completes to verify
    ///     the catalogue-supplied digest.
    /// </summary>
    private static async Task<string> ComputeSha256HexAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    ///     Validate a SHA-256 string. Lowercase hex, 64 chars.
    ///     Throws when the input is malformed so the registry
    ///     fails loud at startup (operator typo in a catalogue
    ///     entry) rather than silently always-rejecting on every
    ///     download.
    /// </summary>
    public static void ValidateSha256(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        if (hex.Length != 64)
        {
            throw new ArgumentException(
                $"SHA-256 must be 64 lowercase hex characters (got {hex.Length}).",
                nameof(hex));
        }

        foreach (var c in hex)
        {
            var isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f');
            if (!isHex)
            {
                throw new ArgumentException(
                    $"SHA-256 must be lowercase hex; got '{c}' at position {hex.IndexOf(c, StringComparison.Ordinal)}.",
                    nameof(hex));
            }
        }
    }
}

/// <summary>
///     Thrown by <see cref="ImageFetcher.DownloadAsync" /> when
///     the catalogue-supplied SHA-256 doesn't match the bytes
///     we just downloaded. Carries both digests so an operator
///     can compare them in the log without re-running.
/// </summary>
public sealed class ImageHashMismatchException(string expected, string actual)
    : Exception(
        string.Create(
            CultureInfo.InvariantCulture,
            $"Image SHA-256 mismatch: expected {expected}, got {actual}."))
{
    /// <summary>Lowercase hex SHA-256 the catalogue declared.</summary>
    public string Expected { get; } = expected;

    /// <summary>Lowercase hex SHA-256 we actually downloaded.</summary>
    public string Actual { get; } = actual;
}

/// <summary>
///     Thrown by <see cref="ImageFetcher.DownloadAsync" /> when
///     the qemu-img convert step fails. The exception message
///     carries the underlying qemu-img stderr.
/// </summary>
public sealed class ImageConversionException(string message)
    : Exception(message);

