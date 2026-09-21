// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderTestController — lifecycle verb for the per-org auth-provider
// configuration (4.6.1, OIDC connection test). Split from the resource
// controller per api-design.md §6 — the test endpoint is not CRUD, it is
// a "is the authority reachable?" probe and shares no parameter shape
// or audit-trail with the read/write endpoints on /auth-provider.
//
// Mounts:
//   POST /api/v1/iam/orgs/{orgId}/auth-provider/test —
//     run the OIDC discovery fetch + parse against the stored authority.
//
// Tenant-scoped: a user in Org X SHALL NOT probe Org Y's auth-provider
// (404 when the URL orgId doesn't match currentUser.TenantId).
//
// Lives in Plexor.Host (not in a separate Plexor.Modules.Realm.Api
// project) for v1 — see OrgAuthProviderController for the rationale.
// ============================================================================

using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plexor.Host.Models;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.AuthProviders;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.AuthProviders;
using Plexor.Shared.Kernel.Identity;

namespace Plexor.Host.Controllers;

/// <summary>
///     Lifecycle endpoint for the per-org auth-provider configuration.
///     Mounted at
///     <c>/api/v1/iam/orgs/{orgId}/auth-provider/test</c> via
///     <see cref="ApiRoutes.Base" />. Tenant-scoped: a user in Org
///     X SHALL NOT probe Org Y's authority (the controller
///     returns 404 when the URL <c>orgId</c> doesn't match
///     <see cref="ICurrentUser.TenantId" />).
/// </summary>
/// <param name="db">Scoped <see cref="RealmDbContext" /> —
/// reads the <c>realm.org_auth_provider_configs</c> table.</param>
/// <param name="currentUser">
/// Scoped <see cref="ICurrentUser" /> — supplies the caller's
/// <c>TenantId</c> for the tenant-scope check.</param>
/// <param name="secretProtector">
/// Singleton <see cref="OrgAuthProviderSecretProtector" /> —
/// purpose-bound wrapper around the host's
/// <c>Microsoft.AspNetCore.DataProtection.IDataProtectionProvider</c>.
/// Encapsulates the purpose string so a different-purpose
/// protector elsewhere can't decrypt the same ciphertext.</param>
/// <param name="httpClientFactory">
/// Scoped <see cref="IHttpClientFactory" /> — used to fetch the
/// OIDC discovery document.</param>
/// <param name="logger">Structured logger.</param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/orgs/{{orgId:guid}}/auth-provider")]
[Tags(["auth-providers"])]
[Authorize]
public sealed class OrgAuthProviderTestController(
    RealmDbContext db,
    ICurrentUser currentUser,
    OrgAuthProviderSecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    ILogger<OrgAuthProviderTestController> logger) : ControllerBase
{
    /// <summary>
    ///     <c>POST /api/v1/iam/orgs/{orgId}/auth-provider/test</c> —
    ///     run the OIDC connection test against the configured
    ///     authority. The endpoint decrypts the stored client
    ///     secret locally (never included in the response) and
    ///     fetches the discovery document at
    ///     <c>{authority}/.well-known/openid-configuration</c>.
    ///     Tenant-scoped: 404 when the URL <c>orgId</c> doesn't
    ///     match the caller's
    ///     <see cref="ICurrentUser.TenantId" />.
    /// </summary>
    /// <param name="orgId">Org id from the URL.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpPost("test", Name = OrgAuthProviderTestControllerRouteNames.ConfigTest)]
    [EndpointSummary("Test the OIDC connection for the per-org auth-provider config")]
    [RequirePermission(AuthProviderPermissions.Update)]
    [ProducesResponseType<OrgAuthProviderTestResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgAuthProviderTestResult>> TestAsync(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        if (orgId != currentUser.TenantId)
        {
            return OrgAuthProviderControllerHelpers.ConfigNotFound(orgId, HttpContext.Request.Path);
        }

        var row = await db.OrgAuthProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(config => config.OrgId == orgId, cancellationToken);

        if (row is null || row.Provider != OrgAuthProvider.Oidc || row.OidcAuthority is null)
        {
            return Problem(
                detail: "Cannot test — provider is not OIDC for this org.",
                instance: HttpContext.Request.Path,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Provider is not OIDC");
        }

        // Decrypt the client secret. The decrypt result is held
        // in memory only for the duration of the HTTP call; it is
        // never logged, never returned in the response, never
        // persisted in plaintext.
        string? plaintextSecret = null;
        if (!string.IsNullOrEmpty(row.OidcClientSecretProtected))
        {
            try
            {
                plaintextSecret = secretProtector.Decrypt(row.OidcClientSecretProtected);
            }
            catch (CryptographicException ex)
            {
                // IDataProtector.Unprotect throws CryptographicException on a
                // keyring rotation or malformed ciphertext — exactly the
                // failure modes that need to surface to the operator who
                // hit the test endpoint. Log + rethrow so the test returns
                // 500 instead of pretending the discovery probe succeeded
                // against an unrecoverable secret.
                logger.LogWarning(
                    ex,
                    "OrgAuthProviderTestController.TestAsync: failed to decrypt the OIDC client secret for org {OrgId}; " +
                    "the keyring may have rotated and the stored ciphertext is unrecoverable.",
                    orgId);
                throw;
            }
        }

        var discoveryUrl = OrgAuthProviderControllerHelpers.BuildDiscoveryDocumentUrl(row.OidcAuthority);
        var fetch = await OrgAuthProviderControllerHelpers.FetchDiscoveryAsync(
            discoveryUrl,
            httpClientFactory,
            cancellationToken);

        var result = new OrgAuthProviderTestResult
        {
            Connected = fetch.Connected,
            DiscoveryDocumentUrl = fetch.Connected ? discoveryUrl : null,
            AvailableScopes = fetch.Scopes ?? [],
            Error = fetch.Error,
        };

        // plaintextSecret is intentionally NOT included in the
        // response. The controller never echoes a decrypted
        // secret — it would be a leak vector. The test is
        // intentionally a "is the authority reachable" probe, not
        // a full PKCE handshake. PKCE lands in 4.6.3.
        _ = plaintextSecret;

        return Ok(result);
    }
}

/// <summary>
///     Stable route names referenced by
///     <c>[HttpPost(..., Name = ...)]</c> and
///     <c>CreatedAtAction(...)</c>. File-scoped so the constants
///     stay local to the file that owns them
///     (constructors-and-fields.md).
/// </summary>
file static class OrgAuthProviderTestControllerRouteNames
{
    /// <summary>POST /api/v1/iam/orgs/{orgId}/auth-provider/test — OIDC connection test.</summary>
    public const string ConfigTest = "org-auth-provider-test";
}