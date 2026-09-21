// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryVirtualMachineRow — wire DTO. VM row in the
// inventory response envelope. Init-property class per
// anti-patterns.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models.Inventory;

/// <summary>
///     VM row in the inventory response envelope.
/// </summary>
public sealed class VSphereInventoryVirtualMachineRow
{
    /// <summary>VM mo-ref (e.g. <c>"vm-1234"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>VM display name.</summary>
    public required string Name { get; init; }

    /// <summary>Inventory folder path; null when at root.</summary>
    public string? FolderPath { get; init; }

    /// <summary>Power state — <c>"POWERED_ON"</c>,
    /// <c>"POWERED_OFF"</c>, <c>"SUSPENDED"</c>.</summary>
    public required string PowerState { get; init; }

    /// <summary>CPU count allocated to the VM.</summary>
    public required int CpuCount { get; init; }

    /// <summary>Memory in MiB allocated to the VM.</summary>
    public required long MemoryMib { get; init; }

    /// <summary>Parent host mo-ref; null for templates.</summary>
    public string? HostMoref { get; init; }
}
