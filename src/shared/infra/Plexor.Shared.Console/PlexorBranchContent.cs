// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorBranchContent — mutable state of PlexorBranchBuilder.
// Properties are public auto-properties because the type is internal
// — see PlexorCliContent for the rationale.
//
// Extracted from CliAppBuilder.cs (Sprint 3, item 3 — 1 type per
// file per folder-organization.md §1).
// ============================================================================

using Spectre.Console.Cli;

namespace Plexor.Shared.Console;

/// <summary>
///     Mutable state of <see cref="PlexorBranchBuilder" />. Properties
///     are <c>public</c> auto-properties because the type is
///     <c>internal</c> — see <see cref="PlexorCliContent" /> for the
///     rationale.
/// </summary>
internal sealed class PlexorBranchContent
{
    /// <summary>The branch name (e.g. <c>"cluster"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Aliases configured via <see cref="PlexorBranchBuilder.WithAlias" />.</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>
    ///     Deferred command configurations, flushed when the parent
    ///     builder's <see cref="PlexorCliBuilder.Run" /> executes.
    /// </summary>
    public List<Action<IConfigurator<CommandSettings>>> PendingConfigurations { get; set; } = [];

    /// <summary>
    ///     Apply the pending command configurations to the supplied
    ///     Spectre branch configurator.
    /// </summary>
    /// <param name="target"></param>
    public void ApplyCommandsTo(IConfigurator<CommandSettings> target)
    {
        foreach (var cfg in PendingConfigurations)
        {
            cfg(target);
        }
    }
}