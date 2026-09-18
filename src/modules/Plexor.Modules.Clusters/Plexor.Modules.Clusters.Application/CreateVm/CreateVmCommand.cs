// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVm — VM-specific provisioning command + result. Mirrors
// the CreateWorkloadCommand / WorkloadSummary pattern but the
// input is VM-shaped (Flavor + Image + Config overlay) and the
// output is the new-workload handle + the node the scheduler
// pinned it to. Application layer is DTO-only; the handler that
// resolves + persists + enqueues lives in
// Plexor.Modules.Clusters.Infrastructure.Clusters.CreateVmHandler.
// ============================================================================

using Plexor.Modules.Clusters.Application.Flavors;
using Plexor.Modules.Clusters.Application.Images;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Application.CreateVm;

/// <summary>
///     Provision a new VM. Resolves
///     <see cref="FlavorName" /> against <see cref="IFlavorCatalog" />,
///     <see cref="ImageName" /> against <see cref="IImageCatalog" />,
///     overlays any <see cref="Config" /> fields, validates the
///     final <see cref="VmRuntimeConfig" />, pins the target node
///     via the placement scheduler, and enqueues a
///     <c>workload.create</c> command for the assigned NodeAgent.
/// </summary>
/// <param name="ClusterId">Target cluster.</param>
/// <param name="Name">Operator-facing VM name (unique per cluster).</param>
/// <param name="FlavorName">
///     Catalog id (e.g. <c>"small"</c>). Null = the handler picks
///     the catalog's default-flavor seed (v0.1 = <c>"small"</c>).
/// </param>
/// <param name="ImageName">
///     Catalog id (e.g. <c>"ubuntu-22.04-cloud"</c>). Null = use
///     the flavor's bundled default image.
/// </param>
/// <param name="Config">
///     Optional overlay over the flavor's
///     <see cref="Flavor.Default" />. Any non-null field wins;
///     null fields fall through to the flavor's value. Set
///     <c>ImageRef</c> in the overlay to override the image
///     without going through the image catalog (rare — the
///     <see cref="ImageName" /> path is preferred).
/// </param>
/// <param name="TargetNodeId">
///     Optional manual pin (see
///     <see cref="Plexor.Modules.Clusters.Application.Abstractions.WorkloadSpec.TargetNodeId" />).
///     Null = "pick for me" — currently maps to "stay unassigned"
///     under the v0.1 manual scheduler.
/// </param>
public sealed record CreateVmCommand(
    ClusterId ClusterId,
    string Name,
    string? FlavorName = null,
    string? ImageName = null,
    VmRuntimeConfig? Config = null,
    NodeId? TargetNodeId = null);

/// <summary>
///     Outcome of a successful <see cref="CreateVmCommand" />.
///     Carries the new workload's wire id and the node the
///     scheduler pinned it to (null when the scheduler couldn't
///     place it — the workload is created in
///     <c>Provisioning</c> state and stays unassigned until the
///     operator pins a node manually).
/// </summary>
/// <param name="WorkloadId">
///     Wire-format workload id (<c>wl_&lt;UUIDv7&gt;</c>). The
///     operator uses this to follow up with start/stop/delete.
/// </param>
/// <param name="AssignedNodeId">
///     Node the scheduler pinned the VM to, or null when the
///     scheduler couldn't place it.
/// </param>
public sealed record CreateVmResult(
    WorkloadId WorkloadId,
    NodeId? AssignedNodeId);