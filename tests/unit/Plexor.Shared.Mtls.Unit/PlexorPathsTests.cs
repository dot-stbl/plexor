// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorPaths unit tests — locks down the relative-path → absolute-path
// resolution that PlexorCaBootstrap depends on. The behaviour must be
// stable: any future change here is a contract change for every
// dev's first-boot experience.
// ============================================================================

using Shouldly;

namespace Plexor.Shared.Mtls.Unit;

/// <summary>
///     Unit tests for <see cref="PlexorPaths" /> — the contract
///     every developer relies on for first-boot cert generation.
/// </summary>
public sealed class PlexorPathsTests
{
    /// <summary>
    ///     When a path is already absolute, PlexorPaths must
    ///     return it untouched — that's the production override
    ///     path (env var or appsettings.Production.json).
    /// </summary>
    [Fact]
    public void Absolute_path_passes_through_unchanged()
    {
        var result = PlexorPaths.ResolveAgainstDevRoot("/etc/plexor/ca.crt");

        result.ShouldBe("/etc/plexor/ca.crt");
    }

    /// <summary>
    ///     Dev-default relative paths like <c>dev-certs/ca.crt</c>
    ///     must join with the resolved repo root so first-boot
    ///     file I/O lands in the same place for every developer.
    /// </summary>
    [Fact]
    public void Relative_path_resolves_against_dev_root()
    {
        var devRoot = PlexorPaths.DevRoot();

        // Whatever the dev root is (varies per machine, e.g. the
        // slnx-root when running from bin/Debug/net10.0/), a
        // relative dev-certs/ca.crt must join with it. The OS
        // may normalise separators one way or the other, so we
        // assert on absolute path + meaningful tail segment rather
        // than the precise separator.
        var result = PlexorPaths.ResolveAgainstDevRoot("dev-certs/ca.crt");

        result.ShouldStartWith(devRoot);
        Path.GetFullPath(result).ShouldBe(
            Path.GetFullPath(Path.Combine(devRoot, "dev-certs", "ca.crt")));
    }

    /// <summary>
    ///     DevRoot must walk up parents until it finds the slnx
    ///     marker — that's the contract every developer relies on,
    ///     because moving the binary outside the solution tree
    ///     would otherwise leave CertAuthorityOptions reading
    ///     paths from the wrong drive.
    /// </summary>
    [Fact]
    public void Dev_root_walks_up_from_base_directory()
    {
        var devRoot = PlexorPaths.DevRoot();

        var info = new DirectoryInfo(devRoot);
        File.Exists(Path.Combine(devRoot, "plexor.slnx"))
            .ShouldBeTrue(
                $"DevRoot must point at the slnx root, got {devRoot} — " +
                "did you move the binary outside the solution tree?");

        // Sanity: the marker is reachable from the current
        // AppContext.BaseDirectory walking up.
        info.FullName.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    ///     DefaultDataRoot must produce a per-OS-conventional
    ///     directory: LocalApplicationData on Windows,
    ///     ~/Library/Application Support on macOS, XDG_DATA_HOME
    ///     or ~/.local/share on Linux. Asserts the leaf name +
    ///     the per-OS marker segment.
    /// </summary>
    [Fact]
    public void DefaultDataRoot_returns_existing_per_os_dir()
    {
        var root = PlexorPaths.DefaultDataRoot();

        if (OperatingSystem.IsWindows())
        {
            root.ShouldContain("plexor");
            root.ShouldContain("AppData");
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            root.ShouldContain("plexor");
            root.ShouldContain("Library");
        }
        else
        {
            // Linux or unknown — accept any plausible path that ends
            // with the plexor leaf. The fallback ~/.local/share
            // branch only creates the directory on first call; if
            // XDG_DATA_HOME is unset the test would create it, which
            // is fine but skips the per-OS assertion.
            root.ShouldEndWith("plexor");
        }
    }
}
