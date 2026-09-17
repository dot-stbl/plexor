// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodesAddSettings — `plx host nodes add`. Inherits the base
// connection options and adds the operator-supplied registration
// fields. `--name` (the new node's hostname) and `--ip` are
// required when this command is invoked. `--role` defaults to
// `Compute` (the common case for new worker nodes).
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx host nodes add</c>. The
///     <c>--join-token</c> is required — the host validates it and
///     mints the row + node-bearer token on success.
/// </summary>
public sealed class HostNodesAddSettings : HostSettings
{
    /// <summary>Hostname for the new node (operator-verifiable).</summary>
    [CommandOption("--name <HOSTNAME>")]
    [Description("Hostname for the new node.")]
    public string? Name { get; init; }

    /// <summary>IP address the agent will be reached at.</summary>
    [CommandOption("--ip <ADDRESS>")]
    [Description("IP address the node agent will be reached at.")]
    public string? Ip { get; init; }

    /// <summary>Role within the cluster. <c>compute</c> = worker; <c>control</c> = control plane.</summary>
    [CommandOption("--role <ROLE>")]
    [Description("Role within the cluster: 'control' or 'compute' (default 'compute').")]
    public string? Role { get; init; }

    /// <summary>JWT-format join token from the host's <c>RotateJoinToken</c>.</summary>
    [CommandOption("--join-token <TOKEN>")]
    [Description("Join token from the host's RotateJoinToken command.")]
    public string? JoinToken { get; init; }
}