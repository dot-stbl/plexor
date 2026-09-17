// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterRotateTokenSettings — `plx cluster rotate-token
// <cluster-id>`. Inherits the connection options from
// HostSettings; takes the cluster id as the positional
// argument. No confirmation prompt — rotation is non-destructive
// to cluster state (the cluster keeps running on its currently-
// joined nodes; only the join token is revoked).
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx cluster rotate-token &lt;cluster-id&gt;</c>.
///     The previous token is revoked the moment the host replies 200;
///     any NodeAgent mid-join that hasn't redeemed the token yet
///     must paste the new one.
/// </summary>
public sealed class ClusterRotateTokenSettings : HostSettings
{
    /// <summary>Target cluster id (wire string, e.g. <c>cluster_&lt;UUIDv7&gt;</c>).</summary>
    [CommandArgument(0, "<CLUSTER_ID>")]
    [Description("Target cluster id (wire string, e.g. cluster_<UUIDv7>).")]
    public string ClusterId { get; init; } = string.Empty;
}