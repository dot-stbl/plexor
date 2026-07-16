// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamController — /iam/users CRUD under [RequirePermission]. Phase 4
// ships only the user surface (Roles + RoleBindings + SSH keys + API
// keys land in subsequent phases).
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Infrastructure.Users;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Sigil.Api.Controllers;

// Route names — referenced by [HttpGet/Post/Patch/Delete(..., Name = ...)]
// and CreatedAtAction(...). CreatedAtAction looks up the action by its
// routing name (the value of `Name =`), NOT by the C# method name;
// `nameof(GetAsync)` fails at runtime with 'Cannot resolve action'. The
// file-scope static class keeps the string in one place per file so
// refactors are safe and the compiler verifies both call sites match.
file static class IamRouteNames
{
    /// <summary>POST /iam/users — create a user.</summary>
    public const string UsersCreate = "iam-users-create";

    /// <summary>GET /iam/users/{userId} — fetch one user.</summary>
    public const string UsersGet = "iam-users-get";

    /// <summary>GET /iam/users — list users.</summary>
    public const string UsersList = "iam-users-list";

    /// <summary>PATCH /iam/users/{userId} — update a user.</summary>
    public const string UsersUpdate = "iam-users-update";

    /// <summary>DELETE /iam/users/{userId} — disable a user.</summary>
    public const string UsersDisable = "iam-users-disable";

    /// <summary>POST /iam/users/{userId}/password — change password.</summary>
    public const string UsersChangePassword = "iam-users-change-password";
}

/// <summary>
///     IAM endpoints for the Sigil module. Mounted at
///     <c>/api/v1/iam/users/*</c> via <see cref="ApiRoutes.Base" />.
///     All endpoints require the appropriate permission claim —
///     callers without it get a 403 ProblemDetails via the global
///     auth handler.
/// </summary>
/// <param name="createHandler"></param>
/// <param name="updateHandler"></param>
/// <param name="disableHandler"></param>
/// <param name="getHandler"></param>
/// <param name="listHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/users")]
[Tags(["iam", "users"])]
[Authorize]
public sealed class IamController(
    CreateUserCommandHandler createHandler,
    UpdateUserCommandHandler updateHandler,
    DisableUserCommandHandler disableHandler,
    ChangePasswordCommandHandler changePasswordHandler,
    GetUserQueryHandler getHandler,
    ListUsersQueryHandler listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/users</c> — create a new user inside the
    ///     caller's organization.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = IamRouteNames.UsersCreate)]
    [EndpointSummary("Create a new user")]
    [RequirePermission("iam.users.create")]
    public async Task<ActionResult<CreateUserResult>> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {

        var result = await createHandler.HandleAsync(
            new CreateUserCommand(
                request.OrgId,
                request.Email,
                request.DisplayName,
                request.Password),
            cancellationToken);

        return CreatedAtAction(
            IamRouteNames.UsersGet,
            new { userId = result.UserId },
            result);
    }

    /// <summary>
    ///     <c>GET /iam/users/{userId}</c> — fetch a single user by id.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet("{userId:guid}", Name = IamRouteNames.UsersGet)]
    [EndpointSummary("Fetch a user by id")]
    [RequirePermission("iam.users.read")]
    public async Task<ActionResult<UserSummary>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var summary = await getHandler.HandleAsync(
            new GetUserQuery(userId),
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    ///     <c>GET /iam/users</c> — list users in the caller's org.
    ///     Paged; <c>?page</c> and <c>?pageSize</c> query params
    ///     default to 1/50.
    /// </summary>
    /// <param name="orgId"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = IamRouteNames.UsersList)]
    [EndpointSummary("List users in the caller's organization")]
    [RequirePermission("iam.users.read")]
    public async Task<ActionResult<UserPage>> ListAsync(
        [FromQuery] Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var pageResult = await listHandler.HandleAsync(
            new ListUsersQuery(orgId, page, pageSize),
            cancellationToken);
        return Ok(pageResult);
    }

    /// <summary>
    ///     <c>PATCH /iam/users/{userId}</c> — update display name and/or
    ///     status. Email changes are rejected (Phase 2+).
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPatch("{userId:guid}", Name = IamRouteNames.UsersUpdate)]
    [EndpointSummary("Update a user's display name and/or status")]
    [RequirePermission("iam.users.update")]
    public async Task<ActionResult<UserSummary>> UpdateAsync(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {

        var summary = await updateHandler.HandleAsync(
            new UpdateUserCommand(userId, request.DisplayName, request.Status),
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    ///     <c>DELETE /iam/users/{userId}</c> — soft-delete a user
    ///     (status = "suspended") and revoke every refresh token
    ///     they own.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    [HttpDelete("{userId:guid}", Name = IamRouteNames.UsersDisable)]
    [EndpointSummary("Disable a user (soft-delete + revoke refresh tokens)")]
    [RequirePermission("iam.users.delete")]
    public async Task<ActionResult<UserSummary>> DisableAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var summary = await disableHandler.HandleAsync(
            new DisableUserCommand(userId),
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    ///     <c>POST /iam/users/{userId}/password</c> — change the
    ///     user's password. The only endpoint the password-rotation
    ///     bearer (single permission
    ///     <c>iam.users.change-own-password</c>) is allowed to call.
    ///     Side effect: every refresh-token family the user owns is
    ///     revoked so a stolen password change can't leave sessions
    ///     alive.
    /// </summary>
    [HttpPost("{userId:guid}/password", Name = IamRouteNames.UsersChangePassword)]
    [EndpointSummary("Change a user's password (current → new) and revoke active sessions")]
    [RequirePermission("iam.users.change-own-password")]
    public async Task<ActionResult<ChangePasswordResult>> ChangePasswordAsync(
        Guid userId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {

        return Ok(await changePasswordHandler.HandleAsync(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken));
    }
}

/// <summary>Wire shape for <c>POST /iam/users</c>.</summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Email">Email address (validated + lowercased on write).</param>
/// <param name="DisplayName">Human-readable name shown in audit + UI.</param>
/// <param name="Password">Initial password (8+ characters).</param>
public sealed record CreateUserRequest(
    Guid OrgId,
    string Email,
    string DisplayName,
    string Password);

/// <summary>Wire shape for <c>PATCH /iam/users/{id}</c>.</summary>
/// <param name="DisplayName">New display name (null = leave unchanged).</param>
/// <param name="Status">New status (null = leave unchanged). Recognised
/// values: <c>"active"</c>, <c>"suspended"</c>.</param>
public sealed record UpdateUserRequest(
    string? DisplayName,
    string? Status);

/// <summary>Wire shape for <c>POST /iam/users/{userId}/password</c>.</summary>
/// <param name="CurrentPassword">Plain-text current password (for
/// verification — same generic error is returned for unknown user
/// to prevent account enumeration).</param>
/// <param name="NewPassword">Plain-text new password (minimum 8
/// characters, validated server-side).</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
