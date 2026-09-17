// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Domain enums + value objects for the Cluster / Node aggregate.
// Plan: .agents/docs/plans/plan-clusters.md. State machines match
// .agents/docs/ui/state-machines.md (Cluster lifecycle) and
// .agents/docs/ui/ui-state-machines.md (Node lifecycle).
//
// NodeRole (formerly here) moved to Plexor.Shared.Identifiers in the
// NodeAgent wire-format alignment — Plexor.Shared.NodeApi references it
// directly to type the RegisterNodeRequest body, so the wire-contract
// enum has to live in shared, not in a module's domain.
// ============================================================================

using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Domain;

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
///     until redeemed, revoked, or expired.
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
///     Aggregate node counts by status. The local <see cref="ClusterNodeSummary" />
///     record (defined in <c>Cluster.cs</c>) carries the integer <c>Status</c> directly
///     so we don't import the Outpost module's NodeStatus enum here.
/// </summary>
public static class NodeCountsExtensions
{
    /// <summary>Total node count across all join-bound clusters.</summary>
    /// <param name="nodes">Nodes to aggregate.</param>
    /// <returns>Counts per status.</returns>
    public static Plexor.Modules.Clusters.Domain.NodeCounts Aggregate(
        IReadOnlyList<ClusterNodeSummary> nodes)
    {
        int ready = 0, pending = 0, gone = 0, draining = 0;
        foreach (var node in nodes)
        {
            var status = (NodeStatus)node.Status;
            if (status == NodeStatus.Ready)
            {
                ready++;
            }
            else if (status == NodeStatus.Pending)
            {
                pending++;
            }
            else if (status == NodeStatus.Gone)
            {
                gone++;
            }
            else if (status == NodeStatus.Draining)
            {
                draining++;
            }
        }

        return new Plexor.Modules.Clusters.Domain.NodeCounts(
            Total: nodes.Count,
            Ready: ready,
            Pending: pending,
            Offline: gone,
            Draining: draining);
    }
}