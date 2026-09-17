// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RegisterNodeResponse — slim agent-facing projection of
// POST /api/v1/nodes/register. Replaces the previous JoinResponse
// (which carried an mTLS cert triple issued by the old Plexor.Host
// join flow). The Outpost flow returns a node-bearer token + cluster
// endpoint instead; the agent only needs the identifiers to start
// heartbeating + polling.
//
// Moved from Plexor.Shared.NodeApi.JoinRequest.cs (Sep 2026).
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Result of a successful <see cref="RegisterNodeRequest" />.
///     The NodeAgent persists <see cref="NodeId" /> +
///     <see cref="ClusterId" /> for use on every subsequent heartbeat
///     + command-poll call.
/// </summary>
/// <param name="NodeId">Plexor node id (wire format <c>node_&lt;UUIDv7&gt;</c>).</param>
/// <param name="ClusterId">
///     Plexor cluster id (wire format <c>cluster_&lt;UUIDv7&gt;</c>) the
///     node now belongs to. The agent echoes this on every heartbeat.
/// </param>
/// <param name="ClusterEndpoint">
///     Post-join rendezvous point. May differ from the configured
///     control-plane URL (behind a reverse proxy, private network,
///     HA failover). v0.1 placeholder for bearer-token auth wiring
///     — the mTLS handler in the agent is not currently using this.
/// </param>
public sealed record RegisterNodeResponse(
    string NodeId,
    string ClusterId,
    string ClusterEndpoint);