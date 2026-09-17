// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BinaryFetcher — fetches a new Plexor.Host binary from a local path
// or an HTTPS URL to a staging file. Used by `plx upgrade`.
//
// v0.1 simplification: no SHA-256 verification, no signed URLs — the
// upgrade trust model relies on the operator having already obtained
// the binary from a trusted source. v0.2 adds
// `--checksum <SHA256>` and `--signature <…>` flags.
// ============================================================================

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     Outcome of a binary fetch. <see cref="ExitCode" /> is 0 on
///     success, non-zero otherwise. <see cref="Detail" /> carries the
///     human-readable diagnostic when the fetch failed.
/// </summary>
/// <param name="ExitCode">0 on success, non-zero on failure.</param>
/// <param name="Detail">Human-readable diagnostic on failure.</param>
public sealed record FetchResult(int ExitCode, string? Detail);

/// <summary>
    ///     Fetch the source binary (URL or local path) to the
    ///     destination. Returns 0 on success, non-zero on failure;
    ///     the result's <see cref="FetchResult.Detail" /> carries the
    ///     human-readable diagnostic.
    /// </summary>
public static class BinaryFetcher
{
    /// <summary>
    ///     <paramref name="source" /> is either an HTTPS / HTTP
    ///     URL (downloaded via HttpClient) or a local file path
    ///     (copied verbatim). The destination is overwritten if it
    ///     already exists.
    /// </summary>
    /// <param name="source">HTTPS URL or local file path.</param>
    /// <param name="destination">Staging path to write the binary to.</param>
    public static async Task<FetchResult> FetchAsync(string source, string destination)
    {
        if (source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return await DownloadAsync(source, destination);
        }

        return await CopyLocalAsync(source, destination);
    }

    private static async Task<FetchResult> DownloadAsync(string url, string destination)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            await using var stream = await client.GetStreamAsync(url);
            await using var file = File.Create(destination);
            await stream.CopyToAsync(file);
            return new FetchResult(0, null);
        }
        catch (Exception ex)
        {
            return new FetchResult(5, $"download failed: {ex.Message}");
        }
    }

    private static Task<FetchResult> CopyLocalAsync(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return Task.FromResult(new FetchResult(5, $"source file not found: {source}"));
        }

        File.Copy(source, destination, overwrite: true);
        return Task.FromResult(new FetchResult(0, null));
    }
}
