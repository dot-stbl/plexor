// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpgradeSettings — Spectre.Console.Cli settings for `plx upgrade`.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx upgrade</c>. The new binary source is
///     required (local path or HTTPS URL).
/// </summary>
public sealed class UpgradeSettings : CommandSettings
{
    /// <summary>Local path or HTTPS URL of the new host binary.</summary>
    [CommandArgument(0, "<SOURCE>")]
    [Description("Local path or HTTPS URL of the new Plexor.Host binary.")]
    public string Source { get; init; } = string.Empty;

    /// <summary>Skip the systemctl restart + health check after the swap.</summary>
    [CommandOption("--no-restart")]
    [Description("Swap binary but don't restart the service or run the health check.")]
    public bool NoRestart { get; init; }
}
