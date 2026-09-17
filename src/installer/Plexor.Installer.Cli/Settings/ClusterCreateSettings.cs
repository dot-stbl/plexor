// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterCreateSettings — `plx cluster create --name <name>`.
// Inherits the connection options from HostSettings; adds the
// required `--name` flag plus optional `--region` and
// `--initial-node-role` flags. The CLI does not currently pass
// `--org` — the v0.1 host controller derives it from
// `Guid.Empty` (single-tenant MVP); Phase 5 follow-up wires it
// through `ICurrentUser`.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx cluster create</c>. <c>--name</c> is
///     required; the rest are optional with sensible defaults.
/// </summary>
public sealed class ClusterCreateSettings : HostSettings
{
    /// <summary>Cluster name (1–128 chars, unique per org).</summary>
    [CommandOption("--name <NAME>")]
    [Description("Cluster name (1-128 chars, unique per org).")]
    public string? Name { get; init; }

    /// <summary>Operator-assigned region (e.g. <c>eu-central-1</c>).</summary>
    [CommandOption("--region <REGION>")]
    [Description("Operator-assigned region label (e.g. eu-central-1).")]
    public string? Region { get; init; }

    /// <summary>
    ///     Role the first joining node will take. <c>control</c> for
    ///     the Plexor.Host itself; <c>compute</c> for worker
    ///     NodeAgents (the common case). Defaults to <c>compute</c>.
    /// </summary>
    [CommandOption("--initial-node-role <ROLE>")]
    [Description("Role the first joining node will take: 'control' or 'compute' (default 'compute').")]
    public string? InitialNodeRole { get; init; }
}