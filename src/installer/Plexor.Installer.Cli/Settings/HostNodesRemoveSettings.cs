// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodesRemoveSettings — `plx host nodes remove <nodeId>`.
// Takes the target node id as the positional argument and adds a
// `--yes` flag to skip the confirmation prompt (CI / scripted
// use). The base connection options come from HostSettings.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx host nodes remove &lt;nodeId&gt;</c>.
///     Confirmation is required by default; pass <c>--yes</c> to
///     skip the prompt.
/// </summary>
public sealed class HostNodesRemoveSettings : HostSettings
{
    /// <summary>Target node id (wire string, e.g. <c>node_&lt;UUIDv7&gt;</c>).</summary>
    [CommandArgument(0, "<NODE_ID>")]
    [Description("Target node id (wire string, e.g. node_<UUIDv7>).")]
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Skip the confirmation prompt.</summary>
    [CommandOption("--yes")]
    [Description("Skip the confirmation prompt (CI / scripted use).")]
    public bool Yes { get; init; }
}