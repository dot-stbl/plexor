// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeListResponse — wire shape for GET /api/v1/nodes.
//
// v0.1 returns a flat array (the dashboard renders a tabular list;
// pagination + filtering come when the FE moves to infinite scroll).
// Phase 5+ may add a PageResult<T> envelope — same as Clusters.list —
// when pagination is needed.
// ============================================================================

namespace Plexor.Modules.Outpost.Api.Models;

/// <summary>Wire shape for <c>GET /api/v1/nodes</c>.</summary>
/// <param name="Nodes">Flat list of node records. Empty when no nodes match.</param>
public sealed record NodeListResponse(
    IReadOnlyList<NodeResponse> Nodes);