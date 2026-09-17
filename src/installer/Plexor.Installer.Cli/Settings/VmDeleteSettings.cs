// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmDeleteSettings — `plx vm delete <vm-id>`. Takes the workload id
// as the positional argument and accepts `--keep-storage` (forward
// compat — v0.1 backend ignores it, the delete always removes the
// runtime handle and storage) and `--yes` to skip the destructive-op
// confirmation prompt (CI / scripted use).
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx vm delete &lt;vm-id&gt;</c>. The vm-id
///     is the workload id wire string. Confirmation is required by
///     default; pass <c>--yes</c> to skip the prompt.
/// </summary>
public sealed class VmDeleteSettings : HostSettings
{
    /// <summary>Target workload id (wire string, e.g. <c>wl_&lt;UUIDv7&gt;</c>).</summary>
    [CommandArgument(0, "<VM_ID>")]
    [Description("Target workload id (wire string, e.g. wl_<UUIDv7>).")]
    public string VmId { get; init; } = string.Empty;

    /// <summary>
    ///     Keep the workload's backing storage after the runtime
    ///     handle is torn down. v0.1 backend ignores the flag (the
    ///     host's <c>DELETE .../workloads/{id}</c> endpoint does
    ///     not accept a keep-storage parameter yet); the CLI keeps
    ///     the flag for forward compatibility.
    /// </summary>
    [CommandOption("--keep-storage")]
    [Description("Keep backing storage after delete (v0.1 backend always removes storage).")]
    public bool KeepStorage { get; init; }

    /// <summary>Skip the confirmation prompt.</summary>
    [CommandOption("--yes")]
    [Description("Skip the confirmation prompt (CI / scripted use).")]
    public bool Yes { get; init; }
}
