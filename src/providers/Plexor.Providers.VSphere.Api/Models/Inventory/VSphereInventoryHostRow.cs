// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryHostRow — wire DTO. Host row in the inventory
// response envelope. Init-property class per anti-patterns.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models.Inventory;

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
