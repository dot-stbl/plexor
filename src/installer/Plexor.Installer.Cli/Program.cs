// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor Installer CLI — `plx` — NativeAOT single-binary installer.
// ============================================================================
// Entry point. Wires the Spectre.Console.Cli command app via the
// shared PlexorCli fluent builder. NativeAOT-friendly: no reflection
// on command types (all command classes are added by closed type
// at startup), no JSON serialization in the hot path.
//
// The CLI exposes a subcommand tree (Group A — installer surface,
// Group B — host operations, Group C — cluster CRUD):
//
//   plx                            — show help (full banner)
//   plx --version                  — print version (full banner)
//   plx --help                     — show help (full banner)
//   plx version                    — same as --version
//   plx init                       — bootstrap a Plexor cluster
//   plx upgrade                    — atomic upgrade
//   plx destroy                    — tear down the cluster on this host
//   plx host status                — overall health of one cluster
//   plx host nodes list            — list registered nodes
//   plx host nodes add             — register a node
//   plx host nodes remove <id>     — unregister a node
//   plx cluster list               — list clusters in the caller's org
//   plx cluster create --name N    — provision a new cluster
//   plx cluster delete <id>        — soft-delete a cluster
//   plx cluster rotate-token <id>  — rotate the join token
//   plx cluster show <id>          — single-cluster detail + nodes
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
        .Version("0.3.0")
        .SetBanner("plexor")
        .AddCommand<VersionCommand>(
            "version",
            BannerArt.Icon.Version,
            "Print version and exit",
            static cmd => cmd.WithAlias("v"))
        .AddCommand<InitCommand>(
            "init",
            BannerArt.Icon.Init,
            "Bootstrap a Plexor cluster on this host",
            static cmd => cmd.WithExample(["init", "--name", "prod-eu-1", "--region", "eu-central-1"]))
        .AddCommand<UpgradeCommand>(
            "upgrade",
            BannerArt.Icon.Upgrade,
            "Atomic in-place upgrade of the host binary",
            static cmd => cmd.WithExample(["upgrade", "/path/to/new-plexor-host"]))
        .AddCommand<DestroyCommand>(
            "destroy",
            BannerArt.Icon.Destroy,
            "Tear down the cluster on this host",
            static cmd => cmd.WithExample(["destroy", "--yes", "--purge"]))
        .AddBranch("host", static host =>
        {
            host.AddCommand<HostStatusCommand>("status", static cmd => cmd.WithDescription("Show overall health of one cluster"));
            host.AddBranch("nodes", static nodes =>
            {
                nodes.AddCommand<HostNodesListCommand>("list", static cmd => cmd.WithDescription("List registered nodes"));
                nodes.AddCommand<HostNodesAddCommand>("add", static cmd => cmd.WithDescription("Register a new node"));
                nodes.AddCommand<HostNodesRemoveCommand>("remove", static cmd => cmd.WithDescription("Unregister a node")
                    .WithExample(["remove", "node_01H...", "--yes"]));
            });
        })
        .AddBranch("cluster", static cluster =>
        {
            cluster.AddCommand<ClusterListCommand>("list", static cmd => cmd
                .WithDescription("List clusters in the caller's org")
                .WithExample(["list", "--page", "1", "--page-size", "25"]));
            cluster.AddCommand<ClusterCreateCommand>("create", static cmd => cmd
                .WithDescription("Provision a new cluster (prints join token once)")
                .WithExample(["create", "--name", "prod-eu-1", "--region", "eu-central-1"]));
            cluster.AddCommand<ClusterShowCommand>("show", static cmd => cmd
                .WithDescription("Single-cluster detail + node list")
                .WithExample(["show", "cluster_01H..."]));
            cluster.AddCommand<ClusterRotateTokenCommand>("rotate-token", static cmd => cmd
                .WithDescription("Rotate the cluster's join token (revokes the old one)")
                .WithExample(["rotate-token", "cluster_01H..."]));
            cluster.AddCommand<ClusterDeleteCommand>("delete", static cmd => cmd
                .WithDescription("Soft-delete a cluster (cascades node status to Gone)")
                .WithExample(["delete", "cluster_01H...", "--yes"]));
        })
        .Run();
