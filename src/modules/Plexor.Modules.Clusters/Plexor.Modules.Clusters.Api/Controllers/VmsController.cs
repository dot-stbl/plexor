// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmsController — POST /api/v1/vms. Flat (not nested under
// /compute/clusters/{clusterId}/workloads) because the wire
// contract is "create a VM"; the cluster id is in the body,
// not the URL. v0.1 ships only Create; Get / List / Delete live
// on the cluster-scoped WorkloadsController until v0.2 lifts
// them to the same flat surface.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Clusters.Api.Models;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Authorization;
using Plexor.Modules.Clusters.Application.CreateVm;
using Plexor.Modules.Clusters.Infrastructure.Clusters;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Api.Controllers;

/// <summary>
///     Route names — referenced by <c>[HttpPost(..., Name = ...)]</c>
///     and <c>CreatedAtAction(...)</c>. CreatedAtAction looks up
///     the action by its routing name (the value of <c>Name =</c>),
///     NOT by the C# method name; <c>nameof(GetAsync)</c> would
///     fail with 'Cannot resolve action'.
/// </summary>
file static class VmRouteNames
{
    /// <summary>POST /api/v1/vms — provision a VM.</summary>
    public const string Create = "vms-create";
}

/// <summary>
///     Flat VM-management endpoint at
///     <c>POST /api/v1/vms</c>. Returns 201 Created with the new
///     workload id + assigned node on success. Mounts at the
///     /vms prefix so v0.1 callers don't have to nest under
///     <c>/compute/clusters/{id}/workloads</c>; v0.2 may consolidate
///     the controllers behind a shared VM-management surface.
/// </summary>
/// <param name="createHandler">Create-vm command handler (issue #6).</param>
[ApiController]
[Route($"{ApiRoutes.Base}/vms")]
[Tags(["compute", "vms"])]
[Authorize]
public sealed class VmsController(
    ICommandHandler<CreateVmCommand, CreateVmResult> createHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /api/v1/vms</c> — provision a new VM. Resolves
    ///     <see cref="CreateVmRequest.Flavor" /> and
    ///     <see cref="CreateVmRequest.Image" /> against the
    ///     catalogs, applies any <see cref="CreateVmRequest.Config" />
    ///     overlay, validates the final
    ///     <see cref="VmRuntimeConfig" />, pins the target node via
    ///     the placement scheduler, persists the Workload row, and
    ///     enqueues a <c>workload.create</c> NodeCommand for the
    ///     assigned NodeAgent. Returns 201 with the new workload id
    ///     + assigned node.
    /// </summary>
    /// <param name="request">Operator-supplied spec.</param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = VmRouteNames.Create)]
    [EndpointSummary("Provision a new virtual machine")]
    [RequirePermission(ClusterPermissions.Update)]
    [ProducesResponseType<CreateVmResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateVmResponse>> CreateAsync(
        [FromBody] CreateVmRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(
            new CreateVmCommand(
                ClusterId: request.ClusterId,
                Name: request.Name,
                FlavorName: request.Flavor,
                ImageName: request.Image,
                Config: request.Config,
                TargetNodeId: request.TargetNodeId),
            cancellationToken);

        var response = new CreateVmResponse(result.WorkloadId, result.AssignedNodeId);
        return CreatedAtAction(
            VmRouteNames.Create,
            new { clusterId = request.ClusterId.Value },
            response);
    }
}