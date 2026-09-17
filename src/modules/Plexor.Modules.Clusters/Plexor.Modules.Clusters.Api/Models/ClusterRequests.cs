// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Cluster request DTOs — wire shapes for the Clusters HTTP API
// (cluster CRUD + token rotation). Node-join / node-heartbeat request
// shapes moved to Plexor.Modules.Outpost as part of the node-tracking
// extraction.
// Separate from controllers per coding/anti-patterns.md §2.
// ==========================================================================

using Plexor.Modules.Clusters.Domain;

namespace Plexor.Modules.Clusters.Api.Models;

/// <summary>Wire shape for <c>POST /api/v1/compute/clusters</c>.</summary>
/// <param name="Name">Cluster name (unique per org, 1–128 chars).</param>
/// <param name="Region">Operator-assigned region (e.g. <c>eu-central-1</c>).</param>
/// <param name="InitialNodeRole">Role the first joining node will take.</param>
public sealed record CreateClusterRequest(
    string Name,
    string Region,
    NodeRole InitialNodeRole);

/// <summary>Wire shape for <c>PATCH /api/v1/compute/clusters/{id}</c>.</summary>
/// <param name="Name">New name (null = leave unchanged).</param>
/// <param name="Region">New region (null = leave unchanged).</param>
public sealed record UpdateClusterRequest(
    string? Name,
    string? Region);