// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamBindingsController — role-binding endpoints. Mounted at
// /api/v1/iam/role-bindings/*. Extracted from IamControllers.cs in issue
// #81 / M3.
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
file static class IamBindingsRouteNames
{
    /// <summary>POST /iam/role-bindings — create a binding.</summary>
    public const string RoleBindingsCreate = "iam-role-bindings-create";

    /// <summary>GET /iam/role-bindings — list bindings.</summary>
    public const string RoleBindingsList = "iam-role-bindings-list";

    /// <summary>DELETE /iam/role-bindings/{bindingId} — remove a binding.</summary>
    public const string RoleBindingsDelete = "iam-role-bindings-delete";
}

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
    CreateRoleBindingCommandHandler createHandler,
    DeleteRoleBindingCommandHandler deleteHandler,
    ListRoleBindingsQueryHandler listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/role-bindings</c> — bind a user to a role
    ///     within an org.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = IamBindingsRouteNames.RoleBindingsCreate)]
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
    [HttpGet(Name = IamBindingsRouteNames.RoleBindingsList)]
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
    [HttpDelete("{bindingId:guid}", Name = IamBindingsRouteNames.RoleBindingsDelete)]
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

/// <summary>Wire shape for <c>POST /iam/role-bindings</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="UserId"></param>
/// <param name="RoleId"></param>
public sealed record CreateRoleBindingRequest(Guid OrgId, Guid UserId, Guid RoleId);
