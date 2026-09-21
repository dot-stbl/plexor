// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryResponse — wire shape for GET /api/v1/vsphere/inventory.
// Init-property classes (per anti-patterns.md §2 — no positional records
// on wire shapes). snake_case property names match the JSON the
// generated OpenAPI document commits to.
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models;

/// <summary>
///     Header row for the inventory read — the snapshot metadata +
///     the per-resource row counts. Returned as <c>snapshot</c> in
///     the top-level response envelope.
/// </summary>
public sealed class VSphereInventorySnapshotHeader
{
    /// <summary>Snapshot id (UUID v7).</summary>
    public required Guid Id { get; init; }

    /// <summary>vCenter identifier the snapshot was pulled from.</summary>
    public required string VcenterMoref { get; init; }

    /// <summary>Number of datacenters in this snapshot.</summary>
    public required int DatacenterCount { get; init; }

    /// <summary>Number of clusters in this snapshot.</summary>
    public required int ClusterCount { get; init; }

    /// <summary>Number of hosts in this snapshot.</summary>
    public required int HostCount { get; init; }

    /// <summary>Number of VMs (non-template) in this snapshot.</summary>
    public required int VirtualMachineCount { get; init; }

    /// <summary>UTC timestamp the snapshot was written.</summary>
    public required DateTimeOffset RefreshedAt { get; init; }
}

/// <summary>
///     Cluster row in the inventory response envelope.
/// </summary>
public sealed class VSphereInventoryClusterRow
{
    /// <summary>Cluster mo-ref (e.g. <c>"domain-c7"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Cluster display name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent datacenter mo-ref.</summary>
    public required string DatacenterMoref { get; init; }

    /// <summary>vSphere <c>drs_enabled</c> — when true, DRS handles
    /// initial placement + load balancing.</summary>
    public required bool DrsEnabled { get; init; }
}

/// <summary>
///     Host row in the inventory response envelope.
/// </summary>
public sealed class VSphereInventoryHostRow
{
    /// <summary>Host mo-ref (e.g. <c>"host-21"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Host display name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent cluster mo-ref.</summary>
    public required string ClusterMoref { get; init; }

    /// <summary>Connection state — <c>"CONNECTED"</c>,
    /// <c>"DISCONNECTED"</c>, <c>"NOT_RESPONDING"</c>.</summary>
    public required string ConnectionState { get; init; }

    /// <summary>Total logical CPU cores (sockets × cores).</summary>
    public required int CpuCores { get; init; }

    /// <summary>Total physical memory in MiB.</summary>
    public required long MemoryMib { get; init; }
}

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

/// <summary>
///     Top-level response for <c>GET /api/v1/vsphere/inventory</c>.
///     Bundles the snapshot header + the per-resource row lists so
///     the caller can render the inventory in a single round trip.
/// </summary>
public sealed class VSphereInventoryResponse
{
    /// <summary>Snapshot header (id, vCenter origin, row counts,
    /// refresh timestamp).</summary>
    public required VSphereInventorySnapshotHeader Snapshot { get; init; }

    /// <summary>Clusters in the snapshot.</summary>
    public required IReadOnlyCollection<VSphereInventoryClusterRow> Clusters { get; init; }

    /// <summary>Hosts in the snapshot.</summary>
    public required IReadOnlyCollection<VSphereInventoryHostRow> Hosts { get; init; }

    /// <summary>VMs in the snapshot (non-template).</summary>
    public required IReadOnlyCollection<VSphereInventoryVirtualMachineRow> VirtualMachines { get; init; }
}
