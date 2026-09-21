// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamRolesController — role CRUD endpoints. Mounted at /api/v1/iam/roles/*.
// All endpoints require their respective permission claim.
// Extracted from IamControllers.cs in issue #81 / M3.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Infrastructure.Users;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Sigil.Api.Controllers;

/// <summary>
/// Route names — referenced by [HttpGet/Post/Patch/Delete(..., Name = ...)]
/// and CreatedAtAction(...). CreatedAtAction looks up the action by its
/// routing name (the value of `Name =`), NOT by the C# method name;
/// `nameof(GetAsync)` fails at runtime with 'Cannot resolve action'. The
/// file-scope static class keeps the string in one place per file so
/// refactors are safe and the compiler verifies both call sites match.
/// </summary>
file static class IamRolesRouteNames
{
    /// <summary>POST /iam/roles — create a custom role.</summary>
    public const string RolesCreate = "iam-roles-create";

    /// <summary>GET /iam/roles/{roleId} — fetch one role.</summary>
    public const string RolesGet = "iam-roles-get";

    /// <summary>GET /iam/roles — list roles.</summary>
    public const string RolesList = "iam-roles-list";

    /// <summary>PATCH /iam/roles/{roleId} — update a role.</summary>
    public const string RolesUpdate = "iam-roles-update";

    /// <summary>DELETE /iam/roles/{roleId} — delete a role.</summary>
    public const string RolesDelete = "iam-roles-delete";
}

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
    CreateRoleCommandHandler createHandler,
    UpdateRoleCommandHandler updateHandler,
    DeleteRoleCommandHandler deleteHandler,
    GetRoleQueryHandler getHandler,
    ListRolesQueryHandler listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/roles</c> — create a custom role.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = IamRolesRouteNames.RolesCreate)]
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
            IamRolesRouteNames.RolesGet,
            new { roleId = result.RoleId },
            result);
    }

    /// <summary>
    ///     <c>GET /iam/roles/{roleId}</c> — fetch a role by id.
    /// </summary>
    /// <param name="roleId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet("{roleId:guid}", Name = IamRolesRouteNames.RolesGet)]
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
    [HttpGet(Name = IamRolesRouteNames.RolesList)]
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
    [HttpPatch("{roleId:guid}", Name = IamRolesRouteNames.RolesUpdate)]
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
    [HttpDelete("{roleId:guid}", Name = IamRolesRouteNames.RolesDelete)]
    [EndpointSummary("Delete a custom role")]
    [RequirePermission("iam.roles.delete")]
    public async Task<ActionResult<DeleteRoleResult>> DeleteAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        return Ok(await deleteHandler.HandleAsync(new DeleteRoleCommand(roleId), cancellationToken));
    }
}

/// <summary>Wire shape for <c>POST /iam/roles</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="Name"></param>
/// <param name="Description"></param>
/// <param name="Permissions"></param>
public sealed record CreateRoleRequest(
    Guid OrgId,
    string Name,
    string? Description,
    IReadOnlyCollection<string> Permissions);

/// <summary>Wire shape for <c>PATCH /iam/roles/{id}</c>.</summary>
/// <param name="Description">New description (null = leave unchanged).</param>
/// <param name="Permissions">New permissions list (null = leave unchanged).</param>
public sealed record UpdateRoleRequest(string? Description, IReadOnlyCollection<string>? Permissions);
