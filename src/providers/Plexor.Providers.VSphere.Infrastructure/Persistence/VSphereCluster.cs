// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereCluster — one row per cached vSphere cluster in
// `outpost.vsphere_clusters`. FK back to the snapshot header so a
// refresh truncates + reloads atomically. vCenter mo-ref + name
// + datacenter + DRS flag — enough for the UI to render a placement
// picker; finer details (resource pools, hosts, networks) live in
// the dedicated tables.
// ============================================================================

namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     Cached vSphere cluster row. Carries the vCenter mo-ref (the
///     stable cross-inventory identifier) + the human-readable name
///     + the parent datacenter + the DRS flag.
/// </summary>
public sealed class VSphereCluster
{
    /// <summary>Row id (UUID v7, PK).</summary>
    public Guid Id { get; init; }

    /// <summary>Snapshot FK. Refresh deletes all rows for a given
    /// snapshot id then reloads.</summary>
    public Guid SnapshotId { get; init; }

    /// <summary>vCenter cluster mo-ref (e.g. <c>"domain-c7"</c>).</summary>
    public string Moref { get; init; } = string.Empty;

    /// <summary>Cluster display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Parent datacenter mo-ref.</summary>
    public string DatacenterMoref { get; init; } = string.Empty;

    /// <summary>Whether vSphere DRS is enabled for this cluster —
    /// when true, vCenter handles initial placement.</summary>
    public bool DrsEnabled { get; init; }

    /// <summary>Navigation back to the snapshot header.</summary>
    public VSphereInventorySnapshot? Snapshot { get; init; }
}
