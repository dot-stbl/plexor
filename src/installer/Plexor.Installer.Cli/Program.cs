// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor Installer CLI — `plx` — NativeAOT single-binary installer.
// ============================================================================
// Entry point. Wires the Spectre.Console.Cli command app via the
// shared PlexorCli fluent builder. NativeAOT-friendly: no reflection
// on command types (all command classes are added by closed type
// at startup), no JSON serialization in the hot path.
//
// The CLI exposes a subcommand tree:
//
//   plx                       — show help (full banner)
//   plx --version             — print version (full banner)
//   plx --help                — show help (full banner)
//   plx version               — same as --version
//   plx init                  — bootstrap a Plexor cluster
//   plx upgrade               — atomic upgrade
//   plx destroy               — tear down the cluster on this host
//
// Each subcommand is a closed type in
// src/installer/Plexor.Installer.Cli/Commands/.
//
// Rule: synchronous top-level Main only. No `await` (VSTHRD200), no
// `.GetAwaiter().GetResult()` (VSTHRD103). The CLI is short-lived
// NativeAOT — async work is dispatched via the command classes
// themselves, which can be AsyncCommand<TSettings>.
// ============================================================================

using Plexor.Installer.Commands;
using Plexor.Shared.Console;

return PlexorCli.New(args)
        .Name("plx")
        .Version("0.2.1")
        .SetBanner("plexor")
        .AddCommand<VersionCommand>(
            "version",
            BannerArt.Icon.Version,
            "Print version and exit",
            cmd => cmd.WithAlias("v"))
        .AddDelegate(
            "init",
            BannerArt.Icon.Init,
            "Bootstrap a Plexor cluster on this host",
            _ => 0) // stub until InitCommand lands
        .AddDelegate(
            "upgrade",
            BannerArt.Icon.Upgrade,
            "Atomic in-place upgrade",
            _ => 0) // stub until UpgradeCommand lands
        .AddDelegate(
            "destroy",
            BannerArt.Icon.Destroy,
            "Tear down the cluster on this host",
            _ => 0) // stub until DestroyCommand lands
        .Run();
