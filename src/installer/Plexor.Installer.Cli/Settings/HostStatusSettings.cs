// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostStatusSettings — `plx host status`. Carries the inherited
// `--host / --token / --cluster` options and adds:
//
//   --all   (flag) list every cluster in the org instead of just
//                  the one named by `--cluster` (Phase 5+; v0.1
//                  ignores the flag and always reports on the
//                  resolved cluster).
//
// The command's table output covers per-node status counts
// (Healthy / Stale / Unhealthy → Pending / Ready / Draining / Gone).
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx host status</c>. Inherits
///     <c>--host / --token / --cluster</c> from
///     <see cref="HostSettings" />.
/// </summary>
public sealed class HostStatusSettings : HostSettings
{
    /// <summary>
    ///     List every cluster in the org (Phase 5+). v0.1 ignores
    ///     the flag — the endpoint is per-cluster and the command
    ///     reports on the cluster resolved by <c>--cluster</c>.
    /// </summary>
    [CommandOption("--all")]
    [Description("List every cluster in the org (Phase 5+; v0.1 reports on the resolved cluster).")]
    public bool All { get; init; }
}