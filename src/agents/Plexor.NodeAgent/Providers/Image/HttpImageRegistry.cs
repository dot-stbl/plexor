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
// image refs (e.g. "ubuntu-22.04-cloud") to a record with URL +
// optional SHA-256 + optional Format. The SHA-256 is the
// authoritative trust anchor (P1 #10); when supplied, every
// download is hashed and mismatches throw before the file
// enters the cache.
//
// v0.2+: signed-manifest catalogue entries will move the
// trust root from the operator-configured URL to the
// control plane's signed image registry.
// ==========================================================================

using Microsoft.Extensions.Options;
using Plexor.NodeAgent.Providers.Image.Fetch;
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
    ///     Map of image ref → download descriptor. Each entry
    ///     carries the URL plus optional SHA-256 / Format. When
    ///     the SHA-256 is set, the registry hashes the
    ///     downloaded bytes and rejects mismatches before
    ///     materialising the cache file. Empty map = every ref
    ///     lookup throws <see cref="UnknownImageException" />.
    /// </summary>
    public Dictionary<string, ImageCatalogueEntry> Catalog { get; set; } = [];
}

/// <summary>
///     One image catalogue entry. Held by
///     <see cref="HttpImageRegistryOptions.Catalog" /> so the
///     fetcher can verify + convert at download time.
/// </summary>
/// <param name="Url">Absolute http/https URL.</param>
/// <param name="Sha256">
///     Optional lowercase hex SHA-256 of the downloaded bytes.
///     When supplied, the fetcher hashes the file and rejects
///     mismatches. Validated via <see cref="ImageFetcher.ValidateSha256" />
///     on construction.
/// </param>
/// <param name="Format">
///     Optional wire-format of the downloaded bytes. Defaults
///     to <see cref="ImageFormat.Qcow2" /> when null — the
///     fetcher skips the conversion step.
/// </param>
public sealed record ImageCatalogueEntry(
    string Url,
    string? Sha256 = null,
    ImageFormat? Format = null)
{
    /// <summary>
    ///     Public parameterless ctor + property-style initialisers
    ///     so the binder can populate <c>Url</c> + <c>Sha256</c> +
    ///     <c>Format</c> from the TOML/JSON catalogue. The
    ///     positional ctor is kept for the in-process registry
    ///     fixture (ImageFetcher tests).
    /// </summary>
    public ImageCatalogueEntry() : this(Url: string.Empty) { }
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
        if (!options.Value.Catalog.TryGetValue(imageRef, out var entry))
        {
            throw new UnknownImageException(imageRef);
        }

        if (string.IsNullOrWhiteSpace(entry.Url))
        {
            // The catalogue entry exists but its URL is missing
            // or empty — operator-config error. Surface as
            // UnknownImage so the failure shape matches every
            // other "image not found" path.
            throw new UnknownImageException(imageRef);
        }

        // Validate the SHA-256 at lookup time, not at
        // download time. Operator typos in the catalogue
        // fail fast on first EnsureLocalAsync rather than
        // half-way through a 2GB download.
        if (!string.IsNullOrWhiteSpace(entry.Sha256))
        {
            ImageFetcher.ValidateSha256(entry.Sha256);
        }

        var cachedPath = ResolveCachePath(imageRef);

        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var source = new ImageSource(
            new Uri(entry.Url),
            ExpectedSha256: entry.Sha256,
            Format: entry.Format);

        logger.LogInformation(
            "HttpImageRegistry: ensuring {Ref} ({Url}, sha256={HasHash}, format={Format})",
            imageRef,
            entry.Url,
            !string.IsNullOrWhiteSpace(entry.Sha256),
            entry.Format?.ToString() ?? "qcow2");

        return await ImageFetcher.DownloadAsync(httpClient, source, cachedPath, cancellationToken);
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
        var safe = string.Concat(imageRef.Where(static c =>
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

        return Path.Combine(options.Value.CacheDirectory, safe + ".qcow2");
    }
}

