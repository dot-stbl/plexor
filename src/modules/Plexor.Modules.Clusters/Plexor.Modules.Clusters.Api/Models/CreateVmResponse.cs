// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVmResponse — wire shape for the POST /api/v1/vms 201 Created
// response. Carries the new workload's wire id and the node the
// scheduler pinned it to (null when the scheduler couldn't place
// it — the operator must pin a node manually).
// ============================================================================

using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Api.Models;

/// <summary>
///     Wire shape for the <c>POST /api/v1/vms</c> 201 Created body.
/// </summary>
/// <param name="WorkloadId">Wire-format workload id (<c>wl_&lt;UUIDv7&gt;</c>).</param>
/// <param name="AssignedNodeId">
///     Node the scheduler pinned the VM to, or null when the
///     scheduler couldn't place it.
/// </param>
public sealed record CreateVmResponse(
    WorkloadId WorkloadId,
    NodeId? AssignedNodeId);
