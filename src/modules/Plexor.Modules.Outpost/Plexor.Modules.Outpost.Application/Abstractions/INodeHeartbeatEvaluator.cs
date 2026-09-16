// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// INodeHeartbeatEvaluator — port for the staleness/health classifier.
//
// Pure function: takes the node's last heartbeat + the configured
// thresholds, returns NodeHealth. Implemented in Infrastructure as
// NodeHeartbeatEvaluator (TimeProvider-driven for unit tests). Used by
// the GET /nodes/{id}/health endpoint — does NOT write to the DB
// (NodeHealth is a derived read, not a persisted field).
// ============================================================================

using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Outpost.Application.Abstractions;

/// <summary>
///     Computes <see cref="NodeHealth" /> from a node's last-heartbeat
///     timestamp and the configured staleness threshold. Pure — no
///     I/O, no side effects.
/// </summary>
/// <remarks>
///     <para><b>Why a port.</b> The thresholds (healthy / unhealthy
///     windows) are configuration-shaped; the evaluator's signature
///     is fixed but the implementation can swap (e.g. for a future
///     per-cluster override) without touching the controller.</para>
/// </remarks>
public interface INodeHeartbeatEvaluator
{
    /// <summary>
    ///     Classify the node's current health status.
    /// </summary>
    /// <param name="nodeId">Target node id.</param>
    /// <param name="lastHeartbeatAt">
    ///     Last heartbeat timestamp (UTC). <c>null</c> means the node
    ///     has never heartbeated → <see cref="NodeHealth.Unhealthy" />.
    /// </param>
    /// <param name="now">Wall-clock anchor (UTC) for the staleness calculation.</param>
    /// <returns>The derived health status.</returns>
    public NodeHealth Evaluate(NodeId nodeId, DateTimeOffset? lastHeartbeatAt, DateTimeOffset now);
}