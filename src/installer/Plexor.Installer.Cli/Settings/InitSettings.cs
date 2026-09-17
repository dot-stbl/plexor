// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InitSettings — Spectre.Console.Cli settings for `plx init`.
// All flags are optional; defaults derive from InstallerPaths.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx init</c>. Flags are optional — defaults
///     come from <c>InstallerPaths</c> + the running binary's path.
/// </summary>
public sealed class InitSettings : CommandSettings
{
    /// <summary>Cluster name (1–128 chars). Defaults to the hostname.</summary>
    [CommandOption("--name <NAME>")]
    [Description("Cluster name (defaults to the host's hostname).")]
    public string? Name { get; init; }

    /// <summary>Operator-assigned region label (e.g. <c>eu-central-1</c>).</summary>
    [CommandOption("--region <REGION>")]
    [Description("Operator-assigned region label (e.g. eu-central-1).")]
    public string? Region { get; init; }

    /// <summary>Skip the systemctl enable/start step.</summary>
    [CommandOption("--no-start")]
    [Description("Write systemd unit + binaries but don't enable or start the service.")]
    public bool NoStart { get; init; }

    /// <summary>Overwrite an existing install (data dir + unit file).</summary>
    [CommandOption("--purge")]
    [Description("Overwrite an existing install (data dir + unit file).")]
    public bool Purge { get; init; }
}
