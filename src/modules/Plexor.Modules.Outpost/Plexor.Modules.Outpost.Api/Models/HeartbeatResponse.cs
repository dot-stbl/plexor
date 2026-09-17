// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HeartbeatResponse — wire shape for the heartbeat ack body.
// Extracted from NodesController.cs per folder-organization.md §1
// (one public type per file).
// ============================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Outpost.Api.Models;

/// <summary>Wire shape for the heartbeat ack.</summary>
/// <param name="NodeId">Echo of the caller's node id.</param>
/// <param name="ClusterStatus">Cluster's current status — drives NodeAgent drain / exit.</param>
/// <param name="ServerTime">Host's UTC now — NodeAgent uses for clock-skew checks.</param>
public sealed record HeartbeatResponse(
    NodeId NodeId,
    ClusterStatus ClusterStatus,
    DateTimeOffset ServerTime);