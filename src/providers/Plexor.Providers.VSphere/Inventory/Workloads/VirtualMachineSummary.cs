// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VirtualMachineSummary — vCenter /api/vcenter/vm wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.Workloads;

/// <summary>
///     VirtualMachine inventory row — Plexor reads the
///     <c>/api/vcenter/vm</c> list and projects the fields the UI
///     needs (name + state + power state + hardware summary).
/// </summary>
public sealed record VirtualMachineSummary
{
    /// <summary>VM mo-ref (e.g. <c>"vm-1234"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>VM display name.</summary>
    public required string Name { get; init; }

    /// <summary>Inventory folder path (e.g. <c>"/Datacenter/vm/Templates"</c>).
    /// </summary>
    public string? FolderPath { get; init; }

    /// <summary>Power state — <c>"POWERED_ON"</c>,
    /// <c>"POWERED_OFF"</c>, <c>"SUSPENDED"</c>.</summary>
    public required string PowerState { get; init; }

    /// <summary>CPU count allocated to the VM.</summary>
    public int CpuCount { get; init; }

    /// <summary>Memory in MiB allocated to the VM.</summary>
    public long MemoryMib { get; init; }

    /// <summary>Parent host mo-ref. Null when the VM is a template
    /// (templates are not bound to a host).</summary>
    public string? HostMoref { get; init; }
}
