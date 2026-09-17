// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterListSettings — `plx cluster list`. Inherits the
// connection options from HostSettings; adds paging flags
// (`--page`, `--page-size`) so the operator can iterate large
// cluster fleets without overwhelming the terminal.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx cluster list</c>. Inherits
///     <c>--host / --token / --cluster</c> from
///     <see cref="HostSettings" />; adds the paging flags.
/// </summary>
public sealed class ClusterListSettings : HostSettings
{
    /// <summary>1-based page index (default 1).</summary>
    [CommandOption("--page <N>")]
    [Description("1-based page index (default 1).")]
    public int Page { get; init; } = 1;

    /// <summary>Items per page (default 25, max 100 — host clamps).</summary>
    [CommandOption("--page-size <N>")]
    [Description("Items per page (default 25, max 100 — host clamps).")]
    public int PageSize { get; init; } = 25;
}