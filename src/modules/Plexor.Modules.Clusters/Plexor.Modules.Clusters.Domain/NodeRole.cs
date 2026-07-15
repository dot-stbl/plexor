// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Domain enums + value objects for the Cluster / Node aggregate.
// Plan: .agents/docs/plans/plan-clusters.md. State machines match
// .agents/docs/ui/state-machines.md (Cluster lifecycle) and
// .agents/docs/ui/ui-state-machines.md (Node lifecycle).
// ============================================================================

using Plexor.Modules.Clusters.Domain.Entities;

namespace Plexor.Modules.Clusters.Domain;

/// <summary>
///     Role a Plexor.NodeAgent fills when joining a cluster. The control
///     role is reserved for the Plexor.Host itself; workers run the
///     compute role. Per-cluster role pinning is done at
///     <see cref="JoinToken.IntendedRole" />.
/// </summary>
public enum NodeRole
{
    /// <summary>
    ///     The Plexor.Host control plane. Only one node per cluster can
    ///     redeem a control-plane join token.
    /// </summary>
    Control = 0,

    /// <summary>
    ///     Worker node — runs Plexor.NodeAgent + user workloads.
    ///     Multiple compute nodes per cluster are expected.
    /// </summary>
    Compute = 1,
}

/// <summary>
///     Lifecycle status of a node within a cluster. See
/// .agents/docs/ui/ui-state-machines.md for the full transition matrix.
/// </summary>
public enum NodeStatus
{
    /// <summary>The node has redeemed a join token but has not
    /// completed the first heartbeat handshake.</summary>
    Pending = 0,

    /// <summary>The node is heartbeating every 30 s and is eligible
    /// for workload scheduling.</summary>
    Ready = 1,

    /// <summary>The node is being drained (workloads migrating
    /// off) but is still heartbeating. New workloads are not
    /// scheduled here.</summary>
    Draining = 2,

    /// <summary>Three consecutive missed heartbeats (90 s) flipped the
    /// node to gone. Workloads that lived on this node are marked
    /// for rescheduling.</summary>
    Gone = 3,
}

/// <summary>
///     Lifecycle status of a cluster. Mirrors the Plexor.Host
/// operational runbook in
/// .agents/docs/operations/install.md#post-install-verification.
/// </summary>
public enum ClusterStatus
{
    /// <summary>Cluster row exists, no node has joined yet.</summary>
    Pending = 0,

    /// <summary>One or more nodes have joined and are
    /// heartbeating.</summary>
    Provisioning = 1,

    /// <summary>All nodes joined are Ready; the cluster is
    /// eligible for workload scheduling.</summary>
    Ready = 2,

    /// <summary>Some nodes are still Ready but at least one has
    /// gone Offline. Operators get a warning in the dashboard.</summary>
    Degraded = 3,

    /// <summary>No node has reported a heartbeat in the last
    /// 90 s. The Plexor.Host is unreachable (or the host process
    /// is dead).</summary>
    Offline = 4,
}

/// <summary>
///     Lifecycle status of a single join token. New tokens are Active
/// until redeemed, revoked, or expired.
/// </summary>
public enum TokenStatus
{
    /// <summary>Token is consumable via the join URL.</summary>
    Active = 0,

    /// <summary>Token was explicitly revoked by an operator (or
    /// rotated by a newer token). Joining with this token returns
    /// 401.</summary>
    Revoked = 1,

    /// <summary>Token's <see cref="JoinToken.ExpiresAt" /> has
    /// passed. Plexor.Host rejects the join with 401.</summary>
    Expired = 2,
}

/// <summary>
///     Hardware spec reported by Plexor.NodeAgent on first join. We
///     persist the snapshot — operators care about what the node
///     reports, not what the host thinks it should be.
/// </summary>
/// <param name="Vcpu">Logical CPU count visible to the kernel.</param>
/// <param name="RamGb">Total RAM in gibibytes (rounded up).</param>
/// <param name="DiskGb">Total block storage in gibibytes reachable
/// from the node (Ceph pool, local LVM, etc.).</param>
/// <param name="Providers">Install providers selected for this
/// node (kvm, lxc, pod, ovs, cilium, ...).</param>
public sealed record NodeSpec(
    int Vcpu,
    int RamGb,
    int DiskGb,
    IReadOnlyList<string> Providers)
{
    /// <summary>Total node count across all join-bound clusters.</summary>
    public static NodeCounts Aggregate(IReadOnlyList<Node> nodes)
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
/// cluster-detail / cluster-list pages and by the Plexor.Host
/// dashboard.
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
