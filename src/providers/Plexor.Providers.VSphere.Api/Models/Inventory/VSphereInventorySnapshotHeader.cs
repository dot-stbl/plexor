// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventorySnapshotHeader — wire DTO. Header row of the
// inventory read — the snapshot metadata + the per-resource row
// counts. Returned as <c>snapshot</c> in the top-level response
// envelope. Init-property class per anti-patterns.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models.Inventory;

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
