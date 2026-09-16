// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Domain enums for the Cluster aggregate. The Node-related types
// (NodeStatus, NodeSpec, NodeCounts) moved to Plexor.Modules.Outpost
// when node tracking was extracted from this module.
// ============================================================================

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
///     Lifecycle status of a cluster. Mirrors the Plexor.Host
///     operational runbook in
///     .agents/docs/operations/install.md#post-install-verification.
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