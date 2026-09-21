// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereVirtualMachine — one row per cached VM (non-template) in
// `outpost.vsphere_virtual_machines`. Power state + name + folder
// path + the parent host. FK back to the snapshot header.
// ============================================================================

namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     Cached vSphere VM row. Carries the vCenter mo-ref + name +
///     folder path + power state + CPU/memory allocations +
///     parent host mo-ref. The host mo-ref is nullable because a
///     template / orphan VM may not be bound to a host.
/// </summary>
public sealed class VSphereVirtualMachine
{
    /// <summary>Row id (UUID v7, PK).</summary>
    public Guid Id { get; init; }

    /// <summary>Snapshot FK.</summary>
    public Guid SnapshotId { get; init; }

    /// <summary>vCenter VM mo-ref.</summary>
    public string Moref { get; init; } = string.Empty;

    /// <summary>VM display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Inventory folder path (e.g.
    /// <c>"/Datacenter/vm/Tenants/Acme"</c>). Null when the VM
    /// lives at the root.</summary>
    public string? FolderPath { get; init; }

    /// <summary>Power state — <c>"POWERED_ON"</c>,
    /// <c>"POWERED_OFF"</c>, <c>"SUSPENDED"</c>.</summary>
    public string PowerState { get; init; } = string.Empty;

    /// <summary>CPU count allocated to the VM.</summary>
    public int CpuCount { get; init; }

    /// <summary>Memory in MiB allocated to the VM.</summary>
    public long MemoryMib { get; init; }

    /// <summary>Parent host mo-ref. Null when the VM is not
    /// bound to a host (e.g. unregistered template).</summary>
    public string? HostMoref { get; init; }

    /// <summary>Navigation back to the snapshot header.</summary>
    public VSphereInventorySnapshot? Snapshot { get; init; }
}
