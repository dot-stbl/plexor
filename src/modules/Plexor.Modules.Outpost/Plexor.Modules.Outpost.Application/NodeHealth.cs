// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHealth — derived status computed by INodeHeartbeatEvaluator.
//
// Separate from NodeStatus (the persisted lifecycle stage). NodeHealth is
// read-only and recomputed on every read of GET /nodes/{id}/health; it
// answers "is this node alive RIGHT NOW?" rather than "what phase is this
// node in?".
// ============================================================================

namespace Plexor.Modules.Outpost.Application;

/// <summary>
///     Derived health status computed from
///     <see cref="NodeRecord.LastHeartbeatAt" /> and the configured
///     staleness threshold. Not persisted — computed on demand.
/// </summary>
public enum NodeHealth
{
    /// <summary>
    ///     Last heartbeat within the healthy window. Node is eligible
    ///     for workload scheduling.
    /// </summary>
    Healthy = 0,

    /// <summary>
    ///     Last heartbeat is older than the healthy window but newer
    ///     than the unhealthy window. Operator should investigate but
    ///     the node has not yet been flipped to <see cref="NodeStatus.Gone" />.
    /// </summary>
    Stale = 1,

    /// <summary>
    ///     No heartbeat has ever been recorded, or the last heartbeat
    ///     is older than the unhealthy window. Node is treated as
    ///     unreachable; <see cref="NodeStatus.Gone" /> is the matching
    ///     persisted state.
    /// </summary>
    Unhealthy = 2,
}