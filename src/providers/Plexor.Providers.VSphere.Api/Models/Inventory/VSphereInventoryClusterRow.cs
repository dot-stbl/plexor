// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryClusterRow — wire DTO. Cluster row in the inventory
// response envelope. Init-property class per anti-patterns.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models.Inventory;

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
