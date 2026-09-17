// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmStopSettings — `plx vm stop <vm-id>`. Takes the workload id as
// the positional argument and accepts `--force` (forward-compat —
// v0.1 backend ignores it, only graceful stop is supported).
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx vm stop &lt;vm-id&gt;</c>. The vm-id is
///     the workload id wire string. <c>--force</c> is accepted for
///     forward compatibility with a future hard-stop endpoint — v0.1
///     always issues a graceful stop; the flag is recorded in the
///     command output so the operator sees whether they expected
///     force or graceful behaviour.
/// </summary>
public sealed class VmStopSettings : HostSettings
{
    /// <summary>Target workload id (wire string, e.g. <c>wl_&lt;UUIDv7&gt;</c>).</summary>
    [CommandArgument(0, "<VM_ID>")]
    [Description("Target workload id (wire string, e.g. wl_<UUIDv7>).")]
    public string VmId { get; init; } = string.Empty;

    /// <summary>
    ///     Hard-stop the workload instead of gracefully shutting it
    ///     down. v0.1 backend ignores the flag (the host's
    ///     <c>POST .../actions/stop</c> endpoint does not accept
    ///     a force parameter yet); the CLI keeps the flag for
    ///     forward compatibility.
    /// </summary>
    [CommandOption("--force")]
    [Description("Hard-stop the workload (v0.1 backend always does a graceful stop).")]
    public bool Force { get; init; }
}
