// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamRolesController + IamBindingsController + IamCredentialsController —
// role / role-binding / api-key / ssh-key CRUD for the Sigil module.
// One file because they're all short and share the same DI shape.
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
    [HttpPost(Name = "iam-roles-create")]
    [EndpointSummary("Create a custom role")]
    [RequirePermission("iam.roles.create")]
    public async Task<ActionResult<CreateRoleResult>> CreateAsync(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await createHandler.HandleAsync(
            new CreateRoleCommand(
                request.OrgId,
                request.Name,
                request.Description,
                request.Permissions),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetAsync),
            new { roleId = result.RoleId },
            result);
    }

    /// <summary>
    ///     <c>GET /iam/roles/{roleId}</c> — fetch a role by id.
    /// </summary>
    /// <param name="roleId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet("{roleId:guid}", Name = "iam-roles-get")]
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
    [HttpGet(Name = "iam-roles-list")]
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
    [HttpPatch("{roleId:guid}", Name = "iam-roles-update")]
    [EndpointSummary("Update a custom role")]
    [RequirePermission("iam.roles.update")]
    public async Task<ActionResult<RoleSummary>> UpdateAsync(
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
    [HttpDelete("{roleId:guid}", Name = "iam-roles-delete")]
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
    [HttpPost(Name = "iam-role-bindings-create")]
    [EndpointSummary("Bind a user to a role")]
    [RequirePermission("iam.role-bindings.create")]
    public async Task<ActionResult<CreateRoleBindingResult>> CreateAsync(
        [FromBody] CreateRoleBindingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
    [HttpGet(Name = "iam-role-bindings-list")]
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
    [HttpDelete("{bindingId:guid}", Name = "iam-role-bindings-delete")]
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

/// <summary>
///     Credential endpoints — API keys + SSH keys. Mounted at
///     <c>/api/v1/iam/users/{userId}/{api-keys|ssh-keys}/*</c>.
///     </summary>
[ApiController]
[Authorize]
public abstract class IamCredentialsControllerBase : ControllerBase
{
    /// <summary>
    ///     Helper: 201 Created pointing at the new key's GET endpoint.
    /// </summary>
    /// <param name="routeName"></param>
    /// <param name="routeValues"></param>
    /// <param name="body"></param>
    protected ActionResult CreatedAtKey(string routeName, object routeValues, object body)
    {
        return CreatedAtAction(
            actionName: "GetAsync",
            routeValues: routeValues,
            value: body);
    }
}

/// <summary>API key CRUD for a user.</summary>
/// <param name="issueHandler"></param>
/// <param name="revokeHandler"></param>
/// <param name="listHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/users/{{userId:guid}}/api-keys")]
[Tags(["iam", "api-keys"])]
public sealed class IamApiKeysController(
    IssueApiKeyCommandHandler issueHandler,
    RevokeApiKeyCommandHandler revokeHandler,
    ListApiKeysQueryHandler listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/users/{userId}/api-keys</c> — issue a new
    ///     API key. Returns the raw secret once.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = "iam-api-keys-issue")]
    [EndpointSummary("Issue a new API key")]
    [RequirePermission("iam.api-keys.create")]
    public async Task<ActionResult<IssueApiKeyResult>> IssueAsync(
        Guid userId,
        [FromBody] IssueApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Ok(await issueHandler.HandleAsync(
            new IssueApiKeyCommand(
                userId,
                request.OrgId,
                request.Name,
                request.Permissions,
                request.ExpiresAtUtc),
            cancellationToken));
    }

    /// <summary>
    ///     <c>GET /iam/users/{userId}/api-keys</c> — list keys (active +
    ///     revoked) for a user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = "iam-api-keys-list")]
    [EndpointSummary("List API keys for a user")]
    [RequirePermission("iam.api-keys.read")]
    public async Task<ActionResult<IReadOnlyCollection<ApiKeySummary>>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await listHandler.HandleAsync(new ListApiKeysQuery(userId), cancellationToken));
    }

    /// <summary>
    ///     <c>DELETE /iam/users/{userId}/api-keys/{keyId}</c> —
    ///     revoke a key.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="keyId"></param>
    /// <param name="cancellationToken"></param>
    [HttpDelete("{keyId:guid}", Name = "iam-api-keys-revoke")]
    [EndpointSummary("Revoke an API key")]
    [RequirePermission("iam.api-keys.delete")]
    public async Task<ActionResult<RevokeApiKeyResult>> RevokeAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        return Ok(await revokeHandler.HandleAsync(new RevokeApiKeyCommand(keyId), cancellationToken));
    }
}

/// <summary>Wire shape for <c>POST /iam/users/{userId}/api-keys</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="Name"></param>
/// <param name="Permissions"></param>
/// <param name="ExpiresAtUtc"></param>
public sealed record IssueApiKeyRequest(
    Guid OrgId,
    string Name,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset? ExpiresAtUtc);

/// <summary>SSH key CRUD for a user.</summary>
/// <param name="addHandler"></param>
/// <param name="revokeHandler"></param>
/// <param name="listHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/users/{{userId:guid}}/ssh-keys")]
[Tags(["iam", "ssh-keys"])]
public sealed class IamSshKeysController(
    AddSshKeyCommandHandler addHandler,
    RevokeSshKeyCommandHandler revokeHandler,
    ListSshKeysQueryHandler listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/users/{userId}/ssh-keys</c> — register an SSH
    ///     public key. Fingerprint computed server-side.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = "iam-ssh-keys-add")]
    [EndpointSummary("Register a new SSH public key")]
    [RequirePermission("iam.ssh-keys.create")]
    public async Task<ActionResult<SshKeySummary>> AddAsync(
        Guid userId,
        [FromBody] AddSshKeyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Ok(await addHandler.HandleAsync(
            new AddSshKeyCommand(userId, request.OrgId, request.Name, request.PublicKey),
            cancellationToken));
    }

    /// <summary>
    ///     <c>GET /iam/users/{userId}/ssh-keys</c> — list SSH keys
    ///     for a user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = "iam-ssh-keys-list")]
    [EndpointSummary("List SSH keys for a user")]
    [RequirePermission("iam.ssh-keys.read")]
    public async Task<ActionResult<IReadOnlyCollection<SshKeySummary>>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await listHandler.HandleAsync(new ListSshKeysQuery(userId), cancellationToken));
    }

    /// <summary>
    ///     <c>DELETE /iam/users/{userId}/ssh-keys/{keyId}</c> —
    ///     revoke an SSH key.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="keyId"></param>
    /// <param name="cancellationToken"></param>
    [HttpDelete("{keyId:guid}", Name = "iam-ssh-keys-revoke")]
    [EndpointSummary("Revoke an SSH key")]
    [RequirePermission("iam.ssh-keys.delete")]
    public async Task<ActionResult<RevokeSshKeyResult>> RevokeAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        return Ok(await revokeHandler.HandleAsync(new RevokeSshKeyCommand(keyId), cancellationToken));
    }
}

/// <summary>Wire shape for <c>POST /iam/users/{userId}/ssh-keys</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="Name"></param>
/// <param name="PublicKey"></param>
public sealed record AddSshKeyRequest(Guid OrgId, string Name, string PublicKey);
