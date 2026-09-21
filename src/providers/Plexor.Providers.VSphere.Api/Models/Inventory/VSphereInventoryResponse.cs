// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryResponse — wire DTO. Top-level response for
// GET /api/v1/vsphere/inventory. Bundles the snapshot header + the
// per-resource row lists so the caller can render the inventory in a
// single round trip. Init-property class per anti-patterns.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models.Inventory;

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
