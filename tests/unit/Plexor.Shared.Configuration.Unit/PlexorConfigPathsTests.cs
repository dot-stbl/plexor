// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Unit tests for PlexorConfigPaths — the user-level config file
// resolver. Cross-OS convention: <UserProfile>/.plexor/config.toml.
// ============================================================================

using Shouldly;

namespace Plexor.Shared.Configuration.Unit;

/// <summary>
///     Tests for <see cref="PlexorConfigPaths" /> — the user-level
///     config resolver.
/// </summary>
public sealed class PlexorConfigPathsTests
{
    /// <summary>
    ///     DefaultConfigRoot returns <c>&lt;UserProfile&gt;/.plexor</c>
    ///     on every OS. No branching — same path on Windows /
    ///     Linux / macOS so operators don't have to remember
    ///     platform-specific locations.
    /// </summary>
    [Fact]
    public void Default_config_root_is_dot_plexor_under_user_profile()
    {
        var profile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        var root = PlexorConfigPaths.DefaultConfigRoot();

        root.ShouldBe(Path.Combine(profile, ".plexor"));
    }

    /// <summary>
    ///     The dot-prefixed folder is created on first call so
    ///     subsequent reads / writes don't have to mkdir.
    /// </summary>
    [Fact]
    public void Default_config_root_is_created_on_disk()
    {
        var root = PlexorConfigPaths.DefaultConfigRoot();

        Directory.Exists(root).ShouldBeTrue(
            $"expected {root} to be created on access");
    }

    /// <summary>
    ///     PLX_CONFIG_FILE override returns the operator-supplied
    ///     path verbatim — that's how tests and CI point at a
    ///     temp file without touching the user's real config.
    /// </summary>
    [Fact]
    public void PLX_CONFIG_FILE_override_takes_precedence()
    {
        var previous = Environment.GetEnvironmentVariable("PLX_CONFIG_FILE");
        try
        {
            Environment.SetEnvironmentVariable(
                "PLX_CONFIG_FILE", "/tmp/custom-plexor.toml");

            PlexorConfigPaths.DefaultConfigFile()
                .ShouldBe("/tmp/custom-plexor.toml");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLX_CONFIG_FILE", previous);
        }
    }

    /// <summary>
    ///     Without the override, the default file path is
    ///     <c>&lt;UserProfile&gt;/.plexor/config.toml</c>.
    /// </summary>
    [Fact]
    public void Default_config_file_is_dot_plexor_config_toml()
    {
        var previous = Environment.GetEnvironmentVariable("PLX_CONFIG_FILE");
        try
        {
            Environment.SetEnvironmentVariable("PLX_CONFIG_FILE", null);

            var profile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            PlexorConfigPaths.DefaultConfigFile()
                .ShouldBe(Path.Combine(profile, ".plexor", "config.toml"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLX_CONFIG_FILE", previous);
        }
    }

    /// <summary>
    ///     FolderName is exposed as a public const so operator
    ///     scripts (rsync backups, .gitignore generation) can use
    ///     the same constant the runtime uses.
    /// </summary>
    [Fact]
    public void FolderName_constant_is_dot_plexor()
    {
        PlexorConfigPaths.FolderName.ShouldBe(".plexor");
    }
}
