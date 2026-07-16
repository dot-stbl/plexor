// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorConfigurationExtensions — single entry-point that wires the
// Plexor config stack on top of an existing IConfigurationBuilder.
//
// Calling convention:
//
//   var builder = WebApplication.CreateBuilder(args);
//   builder.Configuration.AddPlexorConfiguration();
//
// The Plexor layer is appended LAST so its values win over the
// default JSON providers that WebApplication.CreateBuilder wired
// (appsettings.json, appsettings.{Environment}.json, user secrets).
// PLX_* env vars then win over the TOML file, because env is the
// final provider we add.
// ============================================================================

using Microsoft.Extensions.Configuration;

namespace Plexor.Shared.Configuration;

/// <summary>
///     Composition helpers for the Plexor config stack (env + TOML).
/// </summary>
public static class PlexorConfigurationExtensions
{
    /// <summary>
    ///     Append Plexor providers (TOML file + PLX_* env vars) to
    ///     the existing builder, in priority order (later wins).
    ///
    ///     Layers added:
    ///       1. TOML at <see cref="PlexorConfigPaths.DefaultConfigFile" />
    ///          (returns null if PLX_CONFIG_FILE points elsewhere).
    ///       2. PLX_* environment variables — highest priority.
    /// </summary>
    public static IConfigurationBuilder AddPlexorConfiguration(this IConfigurationBuilder builder)
    {
        var tomlPath = PlexorConfigPaths.DefaultConfigFile();
        if (!string.IsNullOrEmpty(tomlPath))
        {
            builder.Add(new PlexorTomlConfigurationSource(tomlPath));
        }

        builder.Add(new PlexorEnvironmentVariablesSource());
        return builder;
    }
}

/// <summary>
///     IConfigurationSource wrapper for the env-var provider. Mirrors
///     the standard IConfigurationSource pattern so the caller
///     uses the regular <c>Add(source)</c> overload.
/// </summary>
public sealed class PlexorEnvironmentVariablesSource(PlexorEnvironmentVariablesProvider? provider = null) : IConfigurationSource
{
    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return provider ?? new PlexorEnvironmentVariablesProvider();
    }
}

/// <summary>
///     IConfigurationSource wrapper for the TOML provider. We pass
///     the file path through to the provider via the constructor —
///     there's no Microsoft.Extensions.Configuration base class to
///     inherit from for the file-source pattern.
/// </summary>
public sealed class PlexorTomlConfigurationSource(string filePath) : IConfigurationSource
{
    /// <summary>
    ///     Absolute path to the TOML file the source reads. Set by
    ///     the constructor; never mutated.
    /// </summary>
    public string FilePath { get; } = filePath;

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new PlexorTomlFileProvider(FilePath);
    }
}