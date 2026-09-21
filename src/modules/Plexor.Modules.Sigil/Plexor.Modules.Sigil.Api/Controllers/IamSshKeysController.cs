// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamSshKeysController — SSH key CRUD for a user. Mounted at
// /api/v1/iam/users/{userId}/ssh-keys/*. Extracted from IamControllers.cs
// in issue #81 / M3.
// ============================================================================

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
file static class IamSshKeysRouteNames
{
    /// <summary>POST /iam/users/{userId}/ssh-keys — add an SSH public key.</summary>
    public const string SshKeysAdd = "iam-ssh-keys-add";

    /// <summary>GET /iam/users/{userId}/ssh-keys — list SSH keys.</summary>
    public const string SshKeysList = "iam-ssh-keys-list";

    /// <summary>DELETE /iam/users/{userId}/ssh-keys/{keyId} — revoke an SSH key.</summary>
    public const string SshKeysRevoke = "iam-ssh-keys-revoke";
}

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
    [HttpPost(Name = IamSshKeysRouteNames.SshKeysAdd)]
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
    [HttpGet(Name = IamSshKeysRouteNames.SshKeysList)]
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
    [HttpDelete("{keyId:guid}", Name = IamSshKeysRouteNames.SshKeysRevoke)]
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

/// <summary>Wire shape for <c>POST /iam/users/{userId}/ssh-keys</c>.</summary>
/// <param name="OrgId"></param>
/// <param name="Name"></param>
/// <param name="PublicKey"></param>
public sealed record AddSshKeyRequest(Guid OrgId, string Name, string PublicKey);
