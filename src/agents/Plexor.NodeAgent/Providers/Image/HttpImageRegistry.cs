// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HttpImageRegistry — IImageRegistry impl that downloads base
// images from a configured URL on first request and caches
// them on local disk. After the first download, subsequent
// EnsureLocalAsync calls return the cached path without hitting
// the network.
//
// Catalogue lives in configuration under
// NodeAgent:Images:Http:Catalog — a map of operator-facing
// image refs (e.g. "ubuntu-22.04-cloud") to absolute URLs. A
// separate NodeAgent:Images:Http:CacheDirectory key controls
// where downloads land.
//
// v0.1: trust on first download. There is no signature
// verification yet — when the control plane gains a signed
// image registry (Phase 2+), this becomes a DownloadOptions
// pass-through with expectedSha256 etc.
// ==========================================================================

using Microsoft.Extensions.Options;
using Plexor.Shared.Compute;

namespace Plexor.NodeAgent.Providers.Image;

/// <summary>
///     Catalogue + cache root for <see cref="HttpImageRegistry" />.
///     Bound from configuration at startup via the standard
///     Microsoft.Extensions.Options pattern. Properties are
///     mutable (not init-only) because OptionsBuilder.Configure
///     runs the binding delegate after object construction,
///     which forbids init-only members.
/// </summary>
public sealed class HttpImageRegistryOptions
{
    /// <summary>
    ///     Absolute path to the directory under which downloaded
    ///     images are stored. The registry creates the directory
    ///     on first download (it doesn't pre-provision) —
    ///     operators can manage lifecycle via tmpfs /
    ///     overlay-fs mount-once patterns if they want the cache
    ///     to evaporate on reboot.
    /// </summary>
    public string CacheDirectory { get; set; } = "/var/lib/plexor/images-cache";

    /// <summary>
    ///     Map of image ref → download URL. Empty map = every
    ///     ref lookup throws <see cref="UnknownImageException" />.
    /// </summary>
    public Dictionary<string, string> Catalog { get; set; } = [];
}

/// <summary>
///     HTTP-backed image registry. Downloads from a configured
///     URL on first request, caches the bytes to
///     <see cref="HttpImageRegistryOptions.CacheDirectory" />,
///     and returns the cached path on every subsequent call.
/// </summary>
/// <param name="options">
///     Bound from configuration
///     (<c>NodeAgent:Images:Http</c>).
/// </param>
/// <param name="httpClientFactory">
///     Factory for the named HttpClient used to download images.
///     The named client is configured separately (with
///     reasonable timeouts / redirect handling) by
///     <see cref="ComputeBackendsInstaller.AddHttpImageRegistry" />.
/// </param>
/// <param name="logger"></param>
public sealed class HttpImageRegistry(
    IOptions<HttpImageRegistryOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<HttpImageRegistry> logger) : IImageRegistry
{
    /// <summary>
    ///     Named HttpClient identifier — registered by
    ///     <see cref="ComputeBackendsInstaller.AddHttpImageRegistry" />.
    /// </summary>
    public const string HttpClientName = "plexor.image-registry";

    /// <inheritdoc />
    public IReadOnlyCollection<string> AvailableImages =>
        options.Value.Catalog.Keys.ToArray() as IReadOnlyCollection<string> ?? [];

    /// <inheritdoc />
    public async Task<string> EnsureLocalAsync(string imageRef, CancellationToken cancellationToken)
    {
        if (!options.Value.Catalog.TryGetValue(imageRef, out var url))
        {
            throw new UnknownImageException(imageRef);
        }

        var cachedPath = ResolveCachePath(imageRef);

        if (File.Exists(cachedPath))
        {
            // Trust the cache. A future revision can verify the
            // cached file's hash against a manifest; for v0.1 the
            // operator-managed URL is the trust root.
            return cachedPath;
        }

        Directory.CreateDirectory(options.Value.CacheDirectory);

        // Download to a .partial file in the cache dir, then
        // rename into place once the stream completes. The
        // rename is atomic on POSIX (the only supported OS for
        // compute nodes) so a reader of the path sees either the
        // old file or the new file — never a half-written one.
        var partialPath = cachedPath + ".partial";
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        logger.LogInformation(
            "HttpImageRegistry: downloading {Ref} from {Url} to {Path}",
            imageRef,
            url,
            cachedPath);

        try
        {
            using var response = await httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var destination = File.Create(partialPath))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            File.Move(partialPath, cachedPath, overwrite: false);
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

        return cachedPath;
    }

    /// <summary>
    ///     Resolve the on-disk cache path for a given image ref.
    ///     Sanitises the ref so it stays within the cache
    ///     directory even if the ref contains path-traversal
    ///     characters (operators configure refs but the contract
    ///     is operator-supplied = trusted; still, no
    ///     traversal-escape is cheaper than auditing the input).
    /// </summary>
    /// <param name="imageRef"></param>
    private string ResolveCachePath(string imageRef)
    {
        // Image refs are operator-supplied strings ("ubuntu-22.04-cloud",
        // "plexor-fw-1.0"). We strip path-separator characters and
        // ".." segments so a stray ref can't escape the cache
        // directory. Underscores and dashes are common and stay;
        // whitespace / control chars never appear in valid refs.
        var safe = string.Concat(imageRef.Where(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' or '.'));

        // Defensive: if sanitisation emptied the string (e.g. ref
        // was "../../etc/passwd"), fall back to a hash so we
        // still produce a unique, cacheable filename.
        if (safe.Length == 0)
        {
            safe = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(imageRef)));
        }

        return Path.Combine(options.Value.CacheDirectory, safe + ".img");
    }
}
