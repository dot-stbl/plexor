// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHeartbeatRequest — wire shape for POST /api/v1/nodes/heartbeat.
//
// Periodic keepalive from a joined node. The body carries the node id
// + cluster id (both in wire format) + fresh hardware snapshot +
// refreshed IP address (DHCP / VPN re-lease). Moved here from
// Plexor.Modules.Outpost.Api.Models in the NodeAgent wire-format
// alignment (Sep 2026) so the agent doesn't have to reference
// host-side types.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>Wire shape for <c>POST /api/v1/nodes/heartbeat</c>.</summary>
/// <param name="NodeId">
///     Caller's own node id, in wire format
///     (<c>node_&lt;UUIDv7&gt;</c>). Parsed into a strongly-typed
///     <c>NodeId</c> via <c>IdParse.ParseNodeId</c>.
/// </param>
/// <param name="ClusterId">
///     Cluster the node belongs to, in wire format
///     (<c>cluster_&lt;UUIDv7&gt;</c>).
/// </param>
/// <param name="Hardware">Fresh hardware snapshot.</param>
/// <param name="IpAddress">Refreshed IP address (DHCP / VPN re-lease).</param>
public sealed record NodeHeartbeatRequest(
    string NodeId,
    string ClusterId,
    NodeHardwareSpec Hardware,
    string IpAddress);