// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHealthResponse — wire shape for GET /api/v1/nodes/{id}/health.
//
// Derived (not persisted): the heartbeat evaluator computes
// Healthy / Stale / Unhealthy from the row's last-heartbeat
// timestamp + the configured threshold windows. The persisted
// NodeStatus (Pending / Ready / Draining / Gone) is a separate
// lifecycle field; health is "is this node alive RIGHT NOW".
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Application;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Outpost.Api.Models;

/// <summary>Wire shape for <c>GET /api/v1/nodes/{id}/health</c>.</summary>
/// <param name="NodeId">Echoed node id.</param>
/// <param name="Status">Persisted lifecycle status.</param>
/// <param name="Health">Derived health classification.</param>
/// <param name="LastHeartbeatAt">Last keepalive timestamp (UTC), null if never.</param>
/// <param name="ServerTime">Host's UTC now — clients can compute staleness locally.</param>
public sealed record NodeHealthResponse(
    NodeId NodeId,
    NodeStatus Status,
    NodeHealth Health,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset ServerTime);