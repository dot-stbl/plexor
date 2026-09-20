// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventorySnapshot — one row per cached inventory refresh in
// `outpost.vsphere_inventory_snapshots`. The header carries the
// refresh timestamp + datacenter count; the per-resource rows live
// in the child tables (Clusters, Hosts, VirtualMachines).
//
// vSphere is a control-plane provider; the cache exists so the API
// surface can answer "list my clusters / hosts / VMs" without hitting
// vCenter on every request. A refresh is a pull-everything read —
// there is no incremental sync in v1 (issue #77 §Iteration 1).
//
// The schema name (`outpost`) follows the architecture theme — first
// real consumer of the planned node-registry schema. See AGENTS.md
// §"Naming: architecture theme vs C# concept".
// ============================================================================

using Plexor.Shared.Kernel.Common;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     Header row for a vSphere inventory refresh. Carries the
///     refresh wall-clock + datacenter count so the API can answer
///     "when was the cache last updated" + "how many datacenters".
/// </summary>
/// <remarks>
///     <para><b>Refresh model.</b> A full refresh truncates the
///     per-resource child tables (clusters / hosts / VMs) and
///     inserts the latest rows in one transaction. The header row
///     stays — the API returns the most recent header when callers
///     ask "give me the current inventory".</para>
///     <para><b>Filterable.</b> The Plexor.Shared.Filtering registry
///     could add <c>VSphereInventorySnapshot</c> later for admin UI
///     timeline views; not registered in v1.</para>
/// </remarks>
public sealed class VSphereInventorySnapshot : ICreatedAt
{
    /// <summary>Snapshot id (UUID v7, PK).</summary>
    public Guid Id { get; init; }

    /// <summary>vCenter mo-ref the snapshot was pulled from. The
    /// provider v1 assumes a single vCenter per install; this
    /// column lets a future multi-vCenter deploy carry the
    /// origin.</summary>
    public string VCenterMoref { get; init; } = string.Empty;

    /// <summary>Number of datacenters visible in this snapshot.</summary>
    public int DatacenterCount { get; init; }

    /// <summary>Number of clusters visible in this snapshot.</summary>
    public int ClusterCount { get; init; }

    /// <summary>Number of hosts visible in this snapshot.</summary>
    public int HostCount { get; init; }

    /// <summary>Number of VMs visible in this snapshot.</summary>
    public int VirtualMachineCount { get; init; }

    /// <summary>UTC wall-clock when the refresh started.</summary>
    public DateTimeOffset RefreshedAt { get; init; }

    /// <summary>UTC row-creation time. Equals
    /// <see cref="RefreshedAt" /> in v1 (snapshots are immutable);
    /// kept so future schema migrations can diverge them.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Navigation: child cluster rows belonging to this
    /// snapshot.</summary>
    public ICollection<VSphereCluster> Clusters { get; init; } = [];

    /// <summary>Navigation: child host rows belonging to this
    /// snapshot.</summary>
    public ICollection<VSphereHost> Hosts { get; init; } = [];

    /// <summary>Navigation: child VM rows belonging to this
    /// snapshot.</summary>
    public ICollection<VSphereVirtualMachine> VirtualMachines { get; init; } = [];
}
