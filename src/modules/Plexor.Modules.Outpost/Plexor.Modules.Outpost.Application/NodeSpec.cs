// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeSpec — hardware snapshot reported by Plexor.NodeAgent.
//
// Pure value object: Vcpu / RamGb / DiskGb / Providers. Persisted as
// JSONB in Postgres (column type `jsonb`) / raw JSON string in InMemory
// via `HasConversion`. The list of providers is a closed set today
// (kvm, lxc, pod, ovs, cilium) — adding one is a catalog change, not
// a schema migration.
// ============================================================================

namespace Plexor.Modules.Outpost.Application;

/// <summary>
///     Hardware spec reported by Plexor.NodeAgent on first join. We
///     persist the snapshot — operators care about what the node
///     reports, not what the host thinks it should be.
/// </summary>
/// <param name="Vcpu">Logical CPU count visible to the kernel.</param>
/// <param name="RamGb">Total RAM in gibibytes (rounded up).</param>
/// <param name="DiskGb">
///     Total block storage in gibibytes reachable from the node (Ceph
///     pool, local LVM, etc.).
/// </param>
/// <param name="Providers">
///     Install providers selected for this node (kvm, lxc, pod, ovs,
///     cilium, ...).
/// </param>
public sealed record NodeSpec(
    int Vcpu,
    int RamGb,
    int DiskGb,
    IReadOnlyList<string> Providers)
{
    /// <summary>
    ///     Total node count across all join-bound clusters — used by
    ///     the dashboard's cluster-detail card. Pure aggregation; no
    ///     I/O.
    /// </summary>
    /// <param name="nodes">Nodes to aggregate.</param>
    public static NodeCounts Aggregate(IReadOnlyList<NodeRecord> nodes)
    {
        var c = new NodeCounts();
        foreach (var n in nodes)
        {
            c.Total++;
            switch (n.Status)
            {
                case NodeStatus.Ready: c.Ready++; break;
                case NodeStatus.Pending: c.Pending++; break;
                case NodeStatus.Gone: c.Offline++; break;
                case NodeStatus.Draining: c.Draining++; break;
            }
        }
        return c;
    }
}

/// <summary>
///     Aggregated counts of nodes by lifecycle status — used by
///     cluster-detail / cluster-list pages and by the Plexor.Host
///     dashboard.
/// </summary>
public sealed class NodeCounts
{
    /// <summary>Total number of nodes across all statuses.</summary>
    public int Total { get; set; }

    /// <summary>Nodes in <see cref="NodeStatus.Ready" />.</summary>
    public int Ready { get; set; }

    /// <summary>Nodes in <see cref="NodeStatus.Pending" />.</summary>
    public int Pending { get; set; }

    /// <summary>Nodes in <see cref="NodeStatus.Gone" />.</summary>
    public int Offline { get; set; }

    /// <summary>Nodes in <see cref="NodeStatus.Draining" />.</summary>
    public int Draining { get; set; }
}