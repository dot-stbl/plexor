// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamRolesController — role CRUD endpoints. Mounted at
// /api/v1/iam/roles/*. All endpoints require their respective
// permission claim.
//
// Extracted from IamControllers.cs (Sprint 3, item 4) per
// folder-organization.md §1.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Sigil.Api.Records;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Infrastructure.Users;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Sigil.Api.Controllers;

/// <summary>
///     Role CRUD endpoints. Mounted at <c>/api/v1/iam/roles/*</c>.
///     All endpoints require their respective permission claim.
/// </summary>
/// <param name="createHandler"></param>
/// <param name="updateHandler"></param>
/// <param name="deleteHandler"></param>
/// <param name="getHandler"></param>
/// <param name="listHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/roles")]
[Tags(["iam", "roles"])]
[Authorize]
public sealed class IamRolesController(
    ICommandHandler<CreateRoleCommand, CreateRoleResult> createHandler,
    ICommandHandler<UpdateRoleCommand, RoleSummary> updateHandler,
    ICommandHandler<DeleteRoleCommand, DeleteRoleResult> deleteHandler,
    ICommandHandler<GetRoleQuery, RoleSummary> getHandler,
    ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>> listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/roles</c> — create a custom role.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = IamRouteNames.RolesCreate)]
    [EndpointSummary("Create a custom role")]
    [RequirePermission("iam.roles.create")]
    public async Task<ActionResult<CreateRoleResult>> CreateAsync(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(
            new CreateRoleCommand(
                request.OrgId,
                request.Name,
                request.Description,
                request.Permissions),
            cancellationToken);

        return CreatedAtAction(
            IamRouteNames.RolesGet,
            new { roleId = result.RoleId },
            result);
    }

    /// <summary>
    ///     <c>GET /iam/roles/{roleId}</c> — fetch a role by id.
    /// </summary>
    /// <param name="roleId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet("{roleId:guid}", Name = IamRouteNames.RolesGet)]
    [EndpointSummary("Fetch a role by id")]
    [RequirePermission("iam.roles.read")]
    public async Task<ActionResult<RoleSummary>> GetAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        return Ok(await getHandler.HandleAsync(new GetRoleQuery(roleId), cancellationToken));
    }

    /// <summary>
    ///     <c>GET /iam/roles</c> — list roles in an org.
    /// </summary>
    /// <param name="orgId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = IamRouteNames.RolesList)]
    [EndpointSummary("List roles in an organization")]
    [RequirePermission("iam.roles.read")]
    public async Task<ActionResult<IReadOnlyCollection<RoleSummary>>> ListAsync(
        [FromQuery] Guid orgId,
        CancellationToken cancellationToken)
    {
        return Ok(await listHandler.HandleAsync(new ListRolesQuery(orgId), cancellationToken));
    }

    /// <summary>
    ///     <c>PATCH /iam/roles/{roleId}</c> — update description and/or
    ///     permissions on a custom role. Built-in roles are
    ///     protected (handler returns 403).
    /// </summary>
    /// <param name="roleId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPatch("{roleId:guid}", Name = IamRouteNames.RolesUpdate)]
    [EndpointSummary("Update a custom role")]
    [RequirePermission("iam.roles.update")]
    public async Task<ActionResult<RoleSummary>> UpdateAsync(
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await updateHandler.HandleAsync(
            new UpdateRoleCommand(roleId, request.Description, request.Permissions),
            cancellationToken));
    }

    /// <summary>
    ///     <c>DELETE /iam/roles/{roleId}</c> — delete a custom role.
    ///     Built-in roles are protected (handler returns 403).
    /// </summary>
    /// <param name="roleId"></param>
    /// <param name="cancellationToken"></param>
    [HttpDelete("{roleId:guid}", Name = IamRouteNames.RolesDelete)]
    [EndpointSummary("Delete a custom role")]
    [RequirePermission("iam.roles.delete")]
    public async Task<ActionResult<DeleteRoleResult>> DeleteAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        return Ok(await deleteHandler.HandleAsync(new DeleteRoleCommand(roleId), cancellationToken));
    }
}
