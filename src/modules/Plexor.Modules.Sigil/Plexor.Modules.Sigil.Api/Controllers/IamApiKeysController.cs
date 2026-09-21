// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamApiKeysController — API key CRUD for a user. Mounted at
// /api/v1/iam/users/{userId}/api-keys/*. Extracted from IamControllers.cs
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
file static class IamApiKeysRouteNames
{
    /// <summary>POST /iam/users/{userId}/api-keys — issue an API key.</summary>
    public const string ApiKeysIssue = "iam-api-keys-issue";

    /// <summary>GET /iam/users/{userId}/api-keys — list API keys.</summary>
    public const string ApiKeysList = "iam-api-keys-list";

    /// <summary>DELETE /iam/users/{userId}/api-keys/{keyId} — revoke an API key.</summary>
    public const string ApiKeysRevoke = "iam-api-keys-revoke";
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
    [HttpPost(Name = IamApiKeysRouteNames.ApiKeysIssue)]
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
    ///     <c>GET /iam/users/{userId}/api-keys</c> — list keys (active +
    ///     revoked) for a user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    [HttpGet(Name = IamApiKeysRouteNames.ApiKeysList)]
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
    [HttpDelete("{keyId:guid}", Name = IamApiKeysRouteNames.ApiKeysRevoke)]
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
