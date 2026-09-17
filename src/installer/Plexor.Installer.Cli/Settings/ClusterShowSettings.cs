// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterShowSettings — `plx cluster show <cluster-id>`. Inherits
// the connection options from HostSettings; takes the cluster id
// as the positional argument. Reads the single-cluster detail
// endpoint with embedded child nodes; no mutation.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx cluster show &lt;cluster-id&gt;</c>.
///     Read-only; no confirmation prompt.
/// </summary>
public sealed class ClusterShowSettings : HostSettings
{
    /// <summary>Target cluster id (wire string, e.g. <c>cluster_&lt;UUIDv7&gt;</c>).</summary>
    [CommandArgument(0, "<CLUSTER_ID>")]
    [Description("Target cluster id (wire string, e.g. cluster_<UUIDv7>).")]
    public string ClusterId { get; init; } = string.Empty;
}