// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereHost — one row per cached ESXi host in
// `outpost.vsphere_hosts`. Connection state + CPU + memory totals
// feed the per-host capacity view in the UI. FK back to the
// snapshot header so refreshes are atomic.
// ============================================================================

namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     Cached vSphere (ESXi) host row. Carries the vCenter mo-ref +
///     connection state + total CPU + total memory.
/// </summary>
public sealed class VSphereHost
{
    /// <summary>Row id (UUID v7, PK).</summary>
    public Guid Id { get; init; }

    /// <summary>Snapshot FK.</summary>
    public Guid SnapshotId { get; init; }

    /// <summary>vCenter host mo-ref.</summary>
    public string Moref { get; init; } = string.Empty;

    /// <summary>Host display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Parent cluster mo-ref.</summary>
    public string ClusterMoref { get; init; } = string.Empty;

    /// <summary>Connection state — <c>"CONNECTED"</c>,
    /// <c>"DISCONNECTED"</c>, <c>"NOT_RESPONDING"</c>.</summary>
    public string ConnectionState { get; init; } = string.Empty;

    /// <summary>Total logical CPU cores.</summary>
    public int CpuCores { get; init; }

    /// <summary>Total physical memory in MiB.</summary>
    public long MemoryMib { get; init; }

    /// <summary>Navigation back to the snapshot header.</summary>
    public VSphereInventorySnapshot? Snapshot { get; init; }
}
