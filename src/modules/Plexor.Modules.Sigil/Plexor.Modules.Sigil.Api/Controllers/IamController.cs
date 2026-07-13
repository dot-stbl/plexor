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

/// <summary>
///     IAM endpoints for the Sigil module. Mounted at
///     <c>/api/v1/iam/users/*</c> via <see cref="ApiRoutes.Base" />.
///     All endpoints require the appropriate permission claim —
///     callers without it get a 403 ProblemDetails via the global
///     auth handler.
/// </summary>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/users")]
[Tags(["iam", "users"])]
[Authorize]
public sealed class IamController(
    CreateUserCommandHandler createHandler,
    UpdateUserCommandHandler updateHandler,
    DisableUserCommandHandler disableHandler,
    GetUserQueryHandler getHandler,
    ListUsersQueryHandler listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/users</c> — create a new user inside the
    ///     caller's organization.
    /// </summary>
    [HttpPost(Name = "iam-users-create")]
    [EndpointSummary("Create a new user")]
    [RequirePermission("iam.users.create")]
    public async Task<ActionResult<CreateUserResult>> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await createHandler.HandleAsync(
            new CreateUserCommand(
                request.OrgId,
                request.Email,
                request.DisplayName,
                request.Password),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetAsync),
            new { userId = result.UserId },
            result);
    }

    /// <summary>
    ///     <c>GET /iam/users/{userId}</c> — fetch a single user by id.
    /// </summary>
    [HttpGet("{userId:guid}", Name = "iam-users-get")]
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
    [HttpGet(Name = "iam-users-list")]
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
    [HttpPatch("{userId:guid}", Name = "iam-users-update")]
    [EndpointSummary("Update a user's display name and/or status")]
    [RequirePermission("iam.users.update")]
    public async Task<ActionResult<UserSummary>> UpdateAsync(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
    [HttpDelete("{userId:guid}", Name = "iam-users-disable")]
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
