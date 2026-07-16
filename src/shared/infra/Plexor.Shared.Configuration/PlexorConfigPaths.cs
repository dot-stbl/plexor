// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorConfigPaths — user-level config location for Plexor.
//
// We use the `.plexor/` dot-directory convention (same as
// `~/.aws/`, `~/.kube/`, `~/.docker/`, `~/.npm/`). Why dot-dir
// instead of the XDG-recommended `~/.config/plexor/`?
//
//   1. One path level (`~/.plexor/` vs `~/.config/plexor/`).
//   2. Identical path on every OS — no Windows / Mac / Linux
//      branching in the docs or in operator scripts.
//   3. Matches the pattern operators already use for CLI tools
//      (aws/kubectl/docker/npm/cargo/terraform/vault).
//   4. Backs up trivially with a `~/.*` glob in rsync/tar.
//
// Layered config lookup (in priority order, last wins):
//   1. appsettings.json (in-tree, dev default)
//   2. <UserProfile>/.plexor/config.toml — user-level overrides
//      (or PLX_CONFIG_FILE if the operator points elsewhere)
//   3. PLX_* environment variables (runtime override)
// ============================================================================

namespace Plexor.Shared.Configuration;

/// <summary>
///     Resolves the OS-conventional user-level config directory
///     for Plexor. Single convention — <c>~/.plexor/</c> on every
///     OS, resolved through <see cref="Environment.SpecialFolder.UserProfile" />.
/// </summary>
public static class PlexorConfigPaths
{
    /// <summary>
    ///     Folder name inside the user profile. Dot-prefixed so
    ///     a `ls ~` doesn't surface it; same convention as
    ///     <c>.ssh</c>, <c>.kube</c>, <c>.aws</c>, <c>.docker</c>.
    /// </summary>
    public const string FolderName = ".plexor";

    /// <summary>
    ///     Default user-level config directory — the user
    ///     profile joined with <c>.plexor</c>. Creates the
    ///     directory on first access so callers don't have to
    ///     worry about it. Returns the absolute path.
    /// </summary>
    public static string DefaultConfigRoot()
    {
        var profile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(profile, FolderName);
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    ///     Default user-level config file path:
    ///     <c>&lt;UserProfile&gt;/.plexor/config.toml</c>. Returns
    ///     the override path verbatim if <c>PLX_CONFIG_FILE</c>
    ///     is set — that's how tests and CI point at a temp file
    ///     instead of touching the user's real config.
    /// </summary>
    public static string DefaultConfigFile()
    {
        var overridePath = Environment.GetEnvironmentVariable("PLX_CONFIG_FILE");
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(DefaultConfigRoot(), "config.toml");
    }
}
