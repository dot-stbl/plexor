// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterSummary — vCenter /api/vcenter/cluster wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.ReadModels;

/// <summary>
///     Compute cluster (vCenter <c>ClusterComputeResource</c>) — a
///     group of ESXi hosts + their shared resource pool. Plexor
///     treats each cluster as a placement target (VMs land in the
///     cluster's default resource pool unless overridden).
/// </summary>
public sealed record ClusterSummary
{
    /// <summary>Cluster mo-ref (e.g. <c>"domain-c7"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Cluster name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent datacenter mo-ref.</summary>
    public required string DatacenterMoref { get; init; }

    /// <summary>vSphere API "drs_enabled" — when true, DRS handles
    /// initial placement + load balancing.</summary>
    public bool DrsEnabled { get; init; }
}
