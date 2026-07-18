// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorPaths — filesystem layout helpers for the Plexor CA + certs.
// Cross-platform: Windows / Linux / macOS. The dev experience must
// be identical for every developer regardless of OS — same
// `dev-certs/` folder, same filenames, same first-boot flow.
// ============================================================================

namespace Plexor.Shared.Mtls;

/// <summary>
///     Resolves the Plexor filesystem layout. Two concepts:
///     1. DevRoot — the repo root, where dev-certs lives. Found
///        by walking up from AppContext.BaseDirectory until we
///        see .git or plexor.slnx.
///     2. DataRoot — the OS-standard per-user data directory
///        (production default). Windows: LocalApplicationData.
///        Linux: $XDG_DATA_HOME or ~/.local/share. macOS:
///        ~/Library/Application Support.
/// </summary>
public static class PlexorPaths
{
    /// <summary>
    ///     Walks up parent directories from
    ///     <see cref="AppContext.BaseDirectory" /> looking for the
    ///     repository root (marker = .git/ OR plexor.slnx).
    ///     Falls back to the current working directory if no
    ///     marker is found within eight hops — that's the dev
    ///     running the binary from somewhere unusual; first-boot
    ///     will fail loudly with "could not create dev-certs",
    ///     which is exactly what we want so the operator
    ///     notices and overrides.
    /// </summary>
    public static string DevRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var hop = 0; hop < 8 && dir is not null; hop++)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "plexor.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return Environment.CurrentDirectory;
    }

    /// <summary>
    ///     Resolves <paramref name="path" /> against
    ///     <see cref="DevRoot" /> iff it is a relative path.
    ///     Absolute paths (typical for production) pass through
    ///     unchanged.
    /// </summary>
    /// <param name="path"></param>
    public static string ResolveAgainstDevRoot(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(DevRoot(), path);
    }

    /// <summary>
    ///     OS-conventional per-user Plexor data directory —
    ///     the production default location for the CA + cert
    ///     files when no override is supplied (env var or
    ///     appsettings.Production.json sets an absolute path).
    ///     The folder resolver creates the directory on first
    ///     call.
    /// </summary>
    public static string DefaultDataRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            Directory.CreateDirectory(localAppData);
            return Path.Combine(localAppData, "plexor");
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            var profile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            var support = Path.Combine(
                profile, "Library", "Application Support");
            Directory.CreateDirectory(support);
            return Path.Combine(support, "plexor");
        }

        // Linux (and anything that doesn't match Win/Mac) follows
        // XDG Base Directory: $XDG_DATA_HOME, falling back to
        // ~/.local/share. See specifications.freedesktop.org/
        // basedir-spec.
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdg))
        {
            Directory.CreateDirectory(xdg);
            return Path.Combine(xdg, "plexor");
        }

        var profilePath = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        var share = Path.Combine(profilePath, ".local", "share");
        Directory.CreateDirectory(share);
        return Path.Combine(share, "plexor");
    }
}
