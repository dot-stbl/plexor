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
//   plx                       — show help (banner + usage)
//   plx --version             — print version
//   plx --help                — show help
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

using Plexor.Installer.Cli.Commands;
using Plexor.Shared.Console;

return PlexorCli.New(args)
    .Name("plx")
    .Version("0.2.1")
    .SetBanner("plexor")
    .AddCommand<VersionCommand>("version", cmd => cmd
        .WithAlias("v")
        .WithDescription("Print version and exit"))
    .AddDelegate("init", _ => 0)   // stub until InitCommand lands
    .AddDelegate("upgrade", _ => 0) // stub until UpgradeCommand lands
    .AddDelegate("destroy", _ => 0) // stub until DestroyCommand lands
    .Run();