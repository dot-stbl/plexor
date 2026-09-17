// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostNodeHealthResponse — wire shape for `GET /api/v1/nodes/{id}/health`.
// Mirrors the controller's `NodeHealthResponse`.
// ============================================================================

namespace Plexor.Installer.Cli.Refit;

/// <summary>Wire shape for <c>GET /api/v1/nodes/{id}/health</c>.</summary>
/// <param name="NodeId">Echoed node id.</param>
/// <param name="Status">Persisted lifecycle status.</param>
/// <param name="Health">Derived health classification.</param>
/// <param name="LastHeartbeatAt">Last keepalive timestamp (UTC), null if never.</param>
/// <param name="ServerTime">Host's UTC now — clients can compute staleness locally.</param>
public sealed record HostNodeHealthResponse(
    string NodeId,
    HostNodeStatus Status,
    HostNodeHealth Health,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset ServerTime);