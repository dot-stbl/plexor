// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmStartSettings — `plx vm start <vm-id>`. Takes the workload id as
// the positional argument; everything else comes from HostSettings.
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx vm start &lt;vm-id&gt;</c>. The vm-id is
///     the workload id the host returns from
///     <c>POST /clusters/{id}/workloads</c> (wire string,
///     e.g. <c>wl_&lt;UUIDv7&gt;</c>).
/// </summary>
public sealed class VmStartSettings : HostSettings
{
    /// <summary>Target workload id (wire string, e.g. <c>wl_&lt;UUIDv7&gt;</c>).</summary>
    [CommandArgument(0, "<VM_ID>")]
    [Description("Target workload id (wire string, e.g. wl_<UUIDv7>).")]
    public string VmId { get; init; } = string.Empty;
}
