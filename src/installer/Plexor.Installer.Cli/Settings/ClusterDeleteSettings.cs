// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterDeleteSettings — `plx cluster delete <cluster-id>`.
// Inherits the connection options from HostSettings; takes the
// cluster id as the positional argument and adds a `--yes`
// confirmation skip (CI / scripted use) plus a `--purge` flag
// that, when implemented by the host, will also wipe the
// cluster's nodes. v0.1 always soft-deletes; the purge behaviour
// arrives in Phase 5+ when the host exposes the bulk-delete
// cascade endpoint.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx cluster delete &lt;cluster-id&gt;</c>.
///     Confirmation is required by default; pass <c>--yes</c>
///     to skip the prompt.
/// </summary>
public sealed class ClusterDeleteSettings : HostSettings
{
    /// <summary>Target cluster id (wire string, e.g. <c>cluster_&lt;UUIDv7&gt;</c>).</summary>
    [CommandArgument(0, "<CLUSTER_ID>")]
    [Description("Target cluster id (wire string, e.g. cluster_<UUIDv7>).")]
    public string ClusterId { get; init; } = string.Empty;

    /// <summary>Skip the confirmation prompt (CI / scripted use).</summary>
    [CommandOption("--yes")]
    [Description("Skip the confirmation prompt (CI / scripted use).")]
    public bool Yes { get; init; }

    /// <summary>
    ///     Also wipe the cluster's nodes (bulk cascade). v0.1 always
    ///     cascades <c>Node.Status = Gone</c> on delete; a Phase 5+
    ///     follow-up lets the host honour a stronger purge that
    ///     drops node rows entirely.
    /// </summary>
    [CommandOption("--purge")]
    [Description("Also wipe the cluster's nodes (Phase 5+ bulk cascade).")]
    public bool Purge { get; init; }
}