// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostSummary — vCenter /api/vcenter/host wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.ReadModels;

/// <summary>
///     ESXi host — a single hypervisor physical server. The Plexor
///     inventory surface exposes host-level CPU + memory totals so the
///     UI can render capacity per host (cluster totals derive from
///     the per-host rows).
/// </summary>
public sealed record HostSummary
{
    /// <summary>Host mo-ref (e.g. <c>"host-21"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Host name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent cluster mo-ref (hosts in vCenter live inside
    /// a cluster; standalone hosts still have a cluster wrapper).</summary>
    public required string ClusterMoref { get; init; }

    /// <summary>Connection state — <c>"CONNECTED"</c>,
    /// <c>"DISCONNECTED"</c>, <c>"NOT_RESPONDING"</c>.</summary>
    public required string ConnectionState { get; init; }

    /// <summary>Total logical CPU cores (sockets × cores).</summary>
    public int CpuCores { get; init; }

    /// <summary>Total physical memory in MiB.</summary>
    public long MemoryMib { get; init; }
}
