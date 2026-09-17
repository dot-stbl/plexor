// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmCreateSettings — `plx vm create`. Inherits the connection options
// from HostSettings and adds the VM shape knobs:
//
//   --cluster       <ID>        parent cluster (required; inherited as optional)
//   --name          <NAME>      operator-facing workload name (required)
//   --image         <REF>       image registry ref (e.g. ubuntu-22.04-cloud) — required
//   --vcpu          <N>         vCPU count (default 2)
//   --ram-mb        <N>         RAM allocation in MiB (default 2048)
//   --disk-gb       <N>         root disk size in GiB (default 20)
//   --target-node   <NODE_ID>   optional manual placement pin (forward-compat; v0.1 host ignores)
//
// The CLI constructs PascalCase JSON matching LibvirtKvmConfig on the
// agent side: {"RamBytes":..., "CpuCores":..., "NetworkName":"default",
// "BaseImageRef":...}. The control plane stores SpecJson verbatim
// and forwards it to the NodeAgent's libvirt-KVM provider, which
// deserialises it with default JsonSerializerOptions (PascalCase).
// ============================================================================

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Plexor.Installer.Cli.Settings;

/// <summary>
///     Settings for <c>plx vm create</c>. The cluster / name / image
///     trio is required; vCPU / RAM / disk-size have sensible
///     defaults that match the libvirt-KVM provider's own defaults.
/// </summary>
public sealed class VmCreateSettings : HostSettings
{
    /// <summary>Operator-facing workload name (unique per cluster).</summary>
    [CommandOption("--name <NAME>")]
    [Description("Workload name (unique per cluster).")]
    public string? Name { get; init; }

    /// <summary>Image registry ref (e.g. <c>ubuntu-22.04-cloud</c>).</summary>
    [CommandOption("--image <REF>")]
    [Description("Image ref as registered in the NodeAgent's IImageRegistry.")]
    public string? Image { get; init; }

    /// <summary>Logical vCPU count. Default 2 (matches LibvirtKvmConfig).</summary>
    [CommandOption("--vcpu <N>")]
    [Description("vCPU count (default 2).")]
    public int Vcpu { get; init; } = 2;

    /// <summary>RAM allocation in MiB. Default 2048 (2 GiB).</summary>
    [CommandOption("--ram-mb <N>")]
    [Description("RAM in MiB (default 2048).")]
    public int RamMb { get; init; } = 2048;

    /// <summary>Root disk size in GiB. Default 20.</summary>
    [CommandOption("--disk-gb <N>")]
    [Description("Root disk size in GiB (default 20).")]
    public int DiskGb { get; init; } = 20;

    /// <summary>
    ///     Optional manual placement pin — when set, the scheduler
    ///     must place this workload on this specific node. v0.1
    ///     backend ignores the pin (the host's CreateWorkloadCommand
    ///     doesn't accept TargetNodeId from the wire); the CLI
    ///     keeps the flag for forward compatibility.
    /// </summary>
    [CommandOption("--target-node <NODE_ID>")]
    [Description("Optional manual placement pin (v0.1 backend ignores; for forward compat).")]
    public string? TargetNode { get; init; }
}
