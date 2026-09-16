// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeResponse — wire shape for GET /api/v1/nodes/{id}.
//
// Public projection of a NodeRecord row. Mirrors the NodeSummary
// class in Plexor.Modules.Outpost.Application.NodeCommands but is
// the Api-layer DTO (separate file per anti-patterns.md §2 — no
// DTO records in the controller file).
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Application;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Outpost.Api.Models;

/// <summary>Wire shape for <c>GET /api/v1/nodes/{id}</c>.</summary>
/// <param name="Id">Node id (node_&lt;UUIDv7&gt;).</param>
/// <param name="ClusterId">Cluster the node belongs to.</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Hostname">OS-reported hostname.</param>
/// <param name="IpAddress">Address the agent wants to be reached at.</param>
/// <param name="Role">Role within the cluster.</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="Hardware">Hardware snapshot.</param>
/// <param name="IsoVersion">ISO image version.</param>
/// <param name="LastHeartbeatAt">Last keepalive timestamp (UTC), null if never.</param>
/// <param name="CreatedAt">Node creation time (UTC).</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record NodeResponse(
    NodeId Id,
    ClusterId ClusterId,
    Guid OrgId,
    string Hostname,
    string IpAddress,
    NodeRole Role,
    NodeStatus Status,
    NodeHardwareSpec Hardware,
    string IsoVersion,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);