// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorConfigPaths — XDG-style user-level config dir for Plexor.
//
// Layered config lookup (in priority order, last wins):
//   1. appsettings.json (in-tree, dev default)
//   2. PLX_CONFIG_FILE env override (absolute path to any TOML/JSON)
//   3. PlexorConfigPaths.DefaultConfigRoot() / config.toml (user-level)
//   4. /etc/plexor/config.toml (system-level, Linux only)
//   5. PLX_* environment variables (runtime override)
//
// Cross-OS conventions:
//   Windows — %APPDATA%\plexor\config.toml       (Roaming)
//   Linux   — $XDG_CONFIG_HOME/plexor/config.toml or
//             ~/.config/plexor/config.toml
//   macOS   — ~/Library/Application Support/plexor/config.toml
// ============================================================================

namespace Plexor.Shared.Configuration;

/// <summary>
///     Resolves the OS-conventional user-level config directory
///     for Plexor. Production overrides come from
///     <c>PLX_CONFIG_FILE</c> env var; tests and dev environments
///     that don't want to touch user state can point
///     <c>PLX_CONFIG_FILE</c> at a temp path instead.
/// </summary>
public static class PlexorConfigPaths
{
    /// <summary>
    ///     Default user-level config directory (no file name).
    ///     Caller appends <c>config.toml</c>. Same tri-OS split as
    ///     the data-root helper in Plexor.Shared.Mtls, but uses
    /// the *config* convention (XDG_CONFIG_HOME on Linux,
    /// ~/Library/Preferences on macOS, Roaming AppData on
    /// Windows) instead of the data convention.
    /// </summary>
    public static string DefaultConfigRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var roaming = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            Directory.CreateDirectory(roaming);
            return Path.Combine(roaming, "plexor");
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            var profile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            var support = Path.Combine(profile, "Library", "Application Support");
            Directory.CreateDirectory(support);
            return Path.Combine(support, "plexor");
        }

        // Linux (and unknown OS that doesn't match Win/Mac).
        // XDG_CONFIG_HOME wins; ~/.config is the fallback.
        // See specifications.freedesktop.org/basedir-spec.
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdg))
        {
            Directory.CreateDirectory(xdg);
            return Path.Combine(xdg, "plexor");
        }

        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        var config = Path.Combine(home, ".config");
        Directory.CreateDirectory(config);
        return Path.Combine(config, "plexor");
    }

    /// <summary>
    ///     Default user-level config file path:
    ///     <c>&lt;DefaultConfigRoot&gt;/config.toml</c>. Returns
    ///     null if the user has set <c>PLX_CONFIG_FILE</c> — in
    ///     that case the caller should use the env override path
    ///     verbatim.
    /// </summary>
    public static string? DefaultConfigFile()
    {
        var overridePath = Environment.GetEnvironmentVariable("PLX_CONFIG_FILE");
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(DefaultConfigRoot(), "config.toml");
    }
}