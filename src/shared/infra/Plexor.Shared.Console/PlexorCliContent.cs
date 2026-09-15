// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCliContent — mutable state of PlexorCliBuilder. Properties are
// public auto-properties because the type is internal — the access
// means "within this assembly, anyone holding a reference may read
// or write these properties". Builder methods provide the typed
// API; the properties themselves are not part of the public surface.
//
// Extracted from CliAppBuilder.cs (Sprint 3, item 3 — 1 type per
// file per folder-organization.md §1).
// ============================================================================

namespace Plexor.Shared.Console;

/// <summary>
///     Mutable state of <see cref="PlexorCliBuilder" />. Internal data
///     shape shared with the branch builder and runner.
/// </summary>
internal sealed class PlexorCliContent
{
    /// <summary>Raw command-line arguments passed to the CLI.</summary>
    public string[] Args { get; set; } = [];

    /// <summary>
    ///     ASCII banner text rendered before the first command.
    ///     <c>null</c> or empty means no banner.
    /// </summary>
    public string? BannerText { get; set; }

    /// <summary>
    ///     Explicit tagline shown under the banner or in the
    ///     compact mark. <c>null</c> means derive from
    ///     <see cref="ClusterName" /> / <see cref="NodeName" /> /
    ///     default.
    /// </summary>
    public string? Tagline { get; set; }

    /// <summary>Program name used in help and error messages.</summary>
    public string? ToolName { get; set; }

    /// <summary>Version string used for <c>--version</c>.</summary>
    public string? ToolVersion { get; set; }

    /// <summary>Cluster context surfaced in the status footer.</summary>
    public string? ClusterName { get; set; }

    /// <summary>Node context surfaced in the status footer.</summary>
    public string? NodeName { get; set; }

    /// <summary>
    ///     Deferred <see cref="Spectre.Console.Cli.IConfigurator" />
    ///     actions, flushed during
    ///     <see cref="PlexorCliBuilder.Run" />.
    /// </summary>
    public List<Action<Spectre.Console.Cli.IConfigurator>> PendingConfigurations { get; set; } = [];

    /// <summary>
    ///     Metadata for every command registered via
    ///     <c>AddCommand</c> / <c>AddDelegate</c>. Used by
    ///     <see cref="PlexorCliBuilder.Run" /> to render the
    ///     help-banner command list without re-parsing the
    ///     Spectre configuration lambdas.
    /// </summary>
    public List<CommandSpec> RegisteredCommands { get; set; } = [];
}
