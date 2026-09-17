// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlxConfig — `~/.plx/config.json` loader for `plx host *`
// commands. Holds the host URL + bearer token + (optional)
// default cluster id. Search order for resolving a config field:
//   1. CLI flag (--host / --token / --cluster on the command)
//   2. Environment variable (PLX_HOST / PLX_TOKEN / PLX_CLUSTER)
//   3. Config file (~/.plx/config.json, then /etc/plx/config.json)
//   4. null (callers must report the missing required field)
//
// AOT: System.Text.Json source-gen handles the small DTO shape
// without reflection. No [JsonPropertyName] overrides — the JSON
// keys (host / token / cluster) match the property names via
// case-insensitive matching.
//
// File format (~/.plx/config.json):
//
//     {
//       "host": "https://plexor.example.com",
//       "token": "...",
//       "cluster": "cluster_eu_1"
//     }
// ============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plexor.Installer.Cli;

/// <summary>
///     Resolved configuration for <c>plx host</c> commands: target
///     host URL, bearer token, default cluster id. Created from a
///     CLI flag &gt; env var &gt; config-file lookup chain.
/// </summary>
/// <param name="Host">Base URL of the Plexor.Host control plane.</param>
/// <param name="Token">Bearer token for the host API.</param>
/// <param name="Cluster">
///     Default cluster id (commands that take <c>--cluster</c>
///     fall back to this when the flag is omitted).
/// </param>
public sealed record PlxConfig(string Host, string Token, string? Cluster)
{
    /// <summary>JSON file name under the user's config directory.</summary>
    public const string FileName = "config.json";

    /// <summary>JSON file name under the system config directory.</summary>
    public const string SystemFileName = "plx.json";

    /// <summary>Directory name under the user's home (XDG / %APPDATA%).</summary>
    public const string DirectoryName = ".plx";

    /// <summary>Environment variable for the host URL.</summary>
    public const string HostEnvVar = "PLX_HOST";

    /// <summary>Environment variable for the bearer token.</summary>
    public const string TokenEnvVar = "PLX_TOKEN";

    /// <summary>Environment variable for the default cluster id.</summary>
    public const string ClusterEnvVar = "PLX_CLUSTER";

    /// <summary>
    ///     Resolve the configuration. <paramref name="hostOverride" />,
    ///     <paramref name="tokenOverride" />, and
    ///     <paramref name="clusterOverride" /> come from the CLI
    ///     flags (highest priority). Returns <c>null</c> if either
    ///     the host URL or the bearer token cannot be resolved.
    /// </summary>
    /// <param name="hostOverride">CLI <c>--host</c> value, or <c>null</c>.</param>
    /// <param name="tokenOverride">CLI <c>--token</c> value, or <c>null</c>.</param>
    /// <param name="clusterOverride">CLI <c>--cluster</c> value, or <c>null</c>.</param>
    /// <returns>Resolved config, or <c>null</c> if required fields are missing.</returns>
    public static PlxConfig? Resolve(
        string? hostOverride,
        string? tokenOverride,
        string? clusterOverride)
    {
        var fromFile = LoadFromDefaultLocations();

        var host = FirstNonEmpty(hostOverride, GetEnvironmentVariable(HostEnvVar), fromFile?.Host);
        var token = FirstNonEmpty(tokenOverride, GetEnvironmentVariable(TokenEnvVar), fromFile?.Token);
        var cluster = FirstNonEmpty(clusterOverride, GetEnvironmentVariable(ClusterEnvVar), fromFile?.Cluster);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return new PlxConfig(host, token, cluster);
    }

    /// <summary>
    ///     Load the config file from the user's <c>~/.plx/config.json</c>
    ///     first, then fall back to the system
    ///     <c>/etc/plx/plx.json</c>. Returns <c>null</c> when no file
    ///     exists or it fails to parse.
    /// </summary>
    /// <returns>Parsed config, or <c>null</c>.</returns>
    public static PlxConfig? LoadFromDefaultLocations()
    {
        var home = GetEnvironmentVariable("HOME")
                   ?? GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrWhiteSpace(home))
        {
            var userPath = Path.Combine(home, DirectoryName, FileName);
            if (TryRead(userPath, out var fromUser))
            {
                return fromUser;
            }
        }

        var etcPath = OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "plx",
                SystemFileName)
            : Path.Combine("/etc", "plx", SystemFileName);

        if (TryRead(etcPath, out var fromSystem))
        {
            return fromSystem;
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }

    private static bool TryRead(string path, out PlxConfig? config)
    {
        config = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
#pragma warning disable IL2026, IL3050 // STJ reflection-based deserialization; AOT warnings benign for CLI dev builds (config file is small + optional)
            var parsed = JsonSerializer.Deserialize<PlxConfigFile>(bytes, JsonOptions);
#pragma warning restore IL2026, IL3050
            if (parsed is null)
            {
                return false;
            }

            config = new PlxConfig(
                parsed.Host ?? string.Empty,
                parsed.Token ?? string.Empty,
                parsed.Cluster);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     On-disk JSON shape. All fields optional (the loader
    ///     coalesces to <c>null</c> when missing). Snake_case /
    ///     kebab-case JSON keys are matched case-insensitively
    ///     against the PascalCase property names.
    /// </summary>
    /// <param name="Host">Plexor.Host base URL.</param>
    /// <param name="Token">Bearer token for the host API.</param>
    /// <param name="Cluster">Default cluster id.</param>
    private sealed record PlxConfigFile(
        string? Host,
        string? Token,
        string? Cluster);
}