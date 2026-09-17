// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeStatus — lifecycle status of a Plexor.NodeAgent.
//
// Outpost owns this enum (was historically in Plexor.Modules.Clusters.Domain
// before the node-tracking extraction). Stored as int in the database
// and round-tripped via `HasConversion<int>()`. The integer values are
// wire-stable — never renumber existing entries.
// ============================================================================

namespace Plexor.Modules.Outpost.Application;

/// <summary>
///     Lifecycle status of a node. See
///     <c>.agents/docs/ui/ui-state-machines.md</c> for the full
///     transition matrix.
/// </summary>
public enum NodeStatus
{
    /// <summary>
    ///     The node has redeemed a join token but has not completed
    ///     the first heartbeat handshake.
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     The node is heartbeating every 30 s and is eligible for
    ///     workload scheduling.
    /// </summary>
    Ready = 1,

    /// <summary>
    ///     The node is being drained (workloads migrating off) but is
    ///     still heartbeating. New workloads are not scheduled here.
    /// </summary>
    Draining = 2,

    /// <summary>
    ///     Three consecutive missed heartbeats (90 s) flipped the node
    ///     to gone. Workloads that lived on this node are marked for
    ///     rescheduling.
    /// </summary>
    Gone = 3,
}