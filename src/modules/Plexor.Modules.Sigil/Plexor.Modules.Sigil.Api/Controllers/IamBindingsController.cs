// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamBindingsController — role-binding endpoints. Mounted at
// /api/v1/iam/role-bindings/*. Route-name constants live with
// IamRolesController (same `iam-` family, shared `iam-roles-*` /
// `iam-role-bindings-*` name space).
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
///     Role-binding endpoints. Mounted at
///     <c>/api/v1/iam/role-bindings/*</c>.
/// </summary>
/// <param name="createHandler"></param>
/// <param name="deleteHandler"></param>
/// <param name="listHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/role-bindings")]
[Tags(["iam", "role-bindings"])]
[Authorize]
public sealed class IamBindingsController(
    ICommandHandler<CreateRoleBindingCommand, CreateRoleBindingResult> createHandler,
    ICommandHandler<DeleteRoleBindingCommand, DeleteRoleBindingResult> deleteHandler,
    ICommandHandler<ListRoleBindingsQuery, IReadOnlyCollection<RoleBindingSummary>> listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/role-bindings</c> — bind a user to a role
    ///     within an org.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = IamRouteNames.RoleBindingsCreate)]
    [EndpointSummary("Bind a user to a role")]
    [RequirePermission("iam.role-bindings.create")]
    public async Task<ActionResult<CreateRoleBindingResult>> CreateAsync(
        [FromBody] CreateRoleBindingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(
            new CreateRoleBindingCommand(request.OrgId, request.UserId, request.RoleId),
            cancellationToken);

        return Created(
            $"/api/v1/iam/role-bindings/{result.BindingId}",
            result);
    }

    /// <summary>
    ///     <c>GET /iam/role-bindings</c> — list the bindings for a
    ///     user. <c>?userId=</c> required.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = IamRouteNames.RoleBindingsList)]
    [EndpointSummary("List role bindings for a user")]
    [RequirePermission("iam.role-bindings.read")]
    public async Task<ActionResult<IReadOnlyCollection<RoleBindingSummary>>> ListAsync(
        [FromQuery] Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await listHandler.HandleAsync(new ListRoleBindingsQuery(userId), cancellationToken));
    }

    /// <summary>
    ///     <c>DELETE /iam/role-bindings/{bindingId}</c> — remove a
    ///     role binding.
    /// </summary>
    /// <param name="bindingId"></param>
    /// <param name="cancellationToken"></param>
    [HttpDelete("{bindingId:guid}", Name = IamRouteNames.RoleBindingsDelete)]
    [EndpointSummary("Remove a role binding")]
    [RequirePermission("iam.role-bindings.delete")]
    public async Task<ActionResult<DeleteRoleBindingResult>> DeleteAsync(
        Guid bindingId,
        CancellationToken cancellationToken)
    {
        return Ok(await deleteHandler.HandleAsync(
            new DeleteRoleBindingCommand(bindingId),
            cancellationToken));
    }
}