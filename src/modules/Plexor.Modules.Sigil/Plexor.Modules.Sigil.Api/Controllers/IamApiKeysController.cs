// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamApiKeysController — API key CRUD for a user. Mounted at
// /api/v1/iam/users/{userId}/api-keys.
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

/// <summary>API key CRUD for a user.</summary>
/// <param name="issueHandler"></param>
/// <param name="revokeHandler"></param>
/// <param name="listHandler"></param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/users/{{userId:guid}}/api-keys")]
[Tags(["iam", "api-keys"])]
public sealed class IamApiKeysController(
    ICommandHandler<IssueApiKeyCommand, IssueApiKeyResult> issueHandler,
    ICommandHandler<RevokeApiKeyCommand, RevokeApiKeyResult> revokeHandler,
    ICommandHandler<ListApiKeysQuery, IReadOnlyCollection<ApiKeySummary>> listHandler) : ControllerBase
{
    /// <summary>
    ///     <c>POST /iam/users/{userId}/api-keys</c> — issue a new
    ///     API key. Returns the raw secret once.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    [HttpPost(Name = IamRouteNames.ApiKeysIssue)]
    [EndpointSummary("Issue a new API key")]
    [RequirePermission("iam.api-keys.create")]
    public async Task<ActionResult<IssueApiKeyResult>> IssueAsync(
        Guid userId,
        [FromBody] IssueApiKeyRequest request,
        CancellationToken cancellationToken)
    {
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
    ///     <c>GET /iam/users/{userId}/api-keys</c> — list keys
    ///     (active + revoked) for a user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = IamRouteNames.ApiKeysList)]
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
    [HttpDelete("{keyId:guid}", Name = IamRouteNames.ApiKeysRevoke)]
    [EndpointSummary("Revoke an API key")]
    [RequirePermission("iam.api-keys.delete")]
    public async Task<ActionResult<RevokeApiKeyResult>> RevokeAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        return Ok(await revokeHandler.HandleAsync(new RevokeApiKeyCommand(userId, keyId), cancellationToken));
    }
}