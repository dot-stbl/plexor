// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamSshKeysController — SSH key CRUD for a user. Mounted at
// /api/v1/iam/users/{userId}/ssh-keys.
//
// Extracted from IamControllers.cs (Sprint 3, item 4) per
// folder-organization.md §1.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Sigil.Api.Records;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Infrastructure.Users;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;

namespace Plexor.Modules.Sigil.Api.Controllers;

/// <summary>SSH key CRUD for a user.</summary>
/// <param name="addHandler"></param>
/// <param name="revokeHandler"></param>
/// <param name="listHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/users/{{userId:guid}}/ssh-keys")]
[Tags(["iam", "ssh-keys"])]
public sealed class IamSshKeysController(
    ICommandHandler<AddSshKeyCommand, SshKeySummary> addHandler,
    ICommandHandler<RevokeSshKeyCommand, RevokeSshKeyResult> revokeHandler,
    ICommandHandler<ListSshKeysQuery, IReadOnlyCollection<SshKeySummary>> listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/users/{userId}/ssh-keys</c> — register an SSH
    ///     public key. Fingerprint computed server-side.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = IamRouteNames.SshKeysAdd)]
    [EndpointSummary("Register a new SSH public key")]
    [RequirePermission("iam.ssh-keys.create")]
    public async Task<ActionResult<SshKeySummary>> AddAsync(
        Guid userId,
        [FromBody] AddSshKeyRequest request,
        CancellationToken cancellationToken)
    {
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
    [HttpGet(Name = IamRouteNames.SshKeysList)]
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
    [HttpDelete("{keyId:guid}", Name = IamRouteNames.SshKeysRevoke)]
    [EndpointSummary("Revoke an SSH key")]
    [RequirePermission("iam.ssh-keys.delete")]
    public async Task<ActionResult<RevokeSshKeyResult>> RevokeAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        return Ok(await revokeHandler.HandleAsync(new RevokeSshKeyCommand(userId, keyId), cancellationToken));
    }
}
