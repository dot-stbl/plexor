// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DestroySettings — Spectre.Console.Cli settings for `plx destroy`.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx destroy</c>. Confirmation is required by
///     default; pass --yes to skip the prompt (CI / scripted use).
/// </summary>
public sealed class DestroySettings : CommandSettings
{
    /// <summary>Skip the confirmation prompt.</summary>
    [CommandOption("--yes")]
    [Description("Skip the confirmation prompt.")]
    public bool Yes { get; init; }

    /// <summary>Also delete the data directory (default: keep for audit).</summary>
    [CommandOption("--purge")]
    [Description("Also delete the data directory (default: keep for audit).")]
    public bool Purge { get; init; }
}
