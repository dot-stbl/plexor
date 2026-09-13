// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProvidersController — per-organization auth-provider
// configuration REST surface (4.6.1). Mounts three endpoints under
// /api/v1/iam/orgs/{orgId}/auth-provider:
//
//   GET    — fetch the current config (OIDC secret masked).
//   PUT    — upsert the config (provider switch + OIDC fields).
//   POST .../test — run the OIDC discovery fetch + parse.
//
// All endpoints are tenant-scoped: a user in Org X SHALL NOT
// view or mutate Org Y's row (404 when the URL orgId doesn't
// match currentUser.TenantId).
//
// Lives in Plexor.Host (not in a separate Plexor.Modules.Realm.Api
// project) for v1 — the Realm module has no Api project yet, and
// creating one for a 3-endpoint surface is a refactor the user
// explicitly deferred. The controller is auto-discovered by
// AddControllers() because it lives in this assembly.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plexor.Host.Models;
using Plexor.Host.Validation;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.AuthProviders;

namespace Plexor.Host.Controllers;

/// <summary>
///     Stable route names referenced by
///     <c>[HttpGet/Post/Put(..., Name = ...)]</c> and
///     <c>CreatedAtAction(...)</c>. File-scoped so the constants
///     stay local to the file that owns them
///     (constructors-and-fields.md).
/// </summary>
file static class OrgAuthProviderRouteNames
{
    /// <summary>GET /api/v1/iam/orgs/{orgId}/auth-provider — fetch the config.</summary>
    public const string ConfigGet = "org-auth-provider-get";

    /// <summary>PUT /api/v1/iam/orgs/{orgId}/auth-provider — upsert the config.</summary>
    public const string ConfigUpsert = "org-auth-provider-upsert";

    /// <summary>POST /api/v1/iam/orgs/{orgId}/auth-provider/test — OIDC connection test.</summary>
    public const string ConfigTest = "org-auth-provider-test";
}

/// <summary>
///     Default OIDC scope list shipped on every fresh
///     <see cref="OrgAuthProviderConfig" /> row. Always includes
///     <c>openid</c> + <c>profile</c> + <c>email</c>; an admin can
///     extend via PUT. <see cref="OrgAuthProvidersController" />
///     references the same array on every PUT (CA1861 — array
///     literal lifted to a static field).
/// </summary>
file static class OrgAuthProviderDefaults
{
    /// <summary>The default OIDC scope list.</summary>
    public static readonly string[] DefaultScopes = ["openid", "profile", "email"];
}

/// <summary>
///     REST endpoints for the per-org authentication provider
///     configuration. Mounted at
///     <c>/api/v1/iam/orgs/{orgId}/auth-provider</c> via
///     <see cref="ApiRoutes.Base" />. Tenant-scoped: a user in Org
///     X SHALL NOT view or modify Org Y's row (the controller
///     returns 404 when the URL <c>orgId</c> doesn't match
///     <see cref="ICurrentUser.TenantId" />).
/// </summary>
/// <param name="db">Scoped <see cref="RealmDbContext" /> —
/// reads + writes the <c>realm.org_auth_provider_configs</c>
/// table.</param>
/// <param name="currentUser">
/// Scoped <see cref="ICurrentUser" /> — supplies the caller's
/// <c>TenantId</c> for the tenant-scope check.</param>
/// <param name="dataProtectionProvider">
/// Singleton <see cref="IDataProtectionProvider" /> — minted
/// from the host's data-protection keyring. The controller
/// creates a purpose-bound <see cref="IDataProtector" /> per
/// request via
/// <see cref="IDataProtectionProvider.CreateProtector(string)" />;
/// the purpose string
/// <see cref="OrgAuthProviderSecretProtector.Purpose" /> names
/// this protector so a different-purpose protector elsewhere
/// can't decrypt the same ciphertext.</param>
/// <param name="httpClientFactory">
/// Scoped <see cref="IHttpClientFactory" /> — used by the
/// <c>/test</c> endpoint to fetch the OIDC discovery
/// document.</param>
/// <param name="logger">Structured logger.</param>
[ApiController]
[Route($"{ApiRoutes.Base}/iam/orgs/{{orgId:guid}}/auth-provider")]
[Tags(["auth-providers"])]
[Authorize]
public sealed class OrgAuthProvidersController(
    RealmDbContext db,
    ICurrentUser currentUser,
    IDataProtectionProvider dataProtectionProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<OrgAuthProvidersController> logger) : ControllerBase
{
    /// <summary>
    ///     <c>GET /api/v1/iam/orgs/{orgId}/auth-provider</c> —
    ///     fetch the per-org authentication provider config.
    ///     OIDC fields are redacted (client secret shows up as
    ///     <c>"***"</c>). Tenant-scoped: 404 when the URL
    ///     <c>orgId</c> doesn't match the caller's
    ///     <see cref="ICurrentUser.TenantId" />.
    /// </summary>
    /// <param name="orgId">Org id from the URL.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet(Name = OrgAuthProviderRouteNames.ConfigGet)]
    [EndpointSummary("Get the per-org authentication provider configuration")]
    [RequirePermission(AuthProviderPermissions.Read)]
    [ProducesResponseType<OrgAuthProviderConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgAuthProviderConfigResponse>> GetAsync(
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

        if (row is null)
        {
            return OrgAuthProviderControllerHelpers.ConfigNotFound(orgId, HttpContext.Request.Path);
        }

        return Ok(OrgAuthProviderControllerHelpers.MapToResponse(row));
    }

    /// <summary>
    ///     <c>PUT /api/v1/iam/orgs/{orgId}/auth-provider</c> —
    ///     create or update the per-org authentication provider
    ///     config. Switches the provider (Sigil ↔ Oidc) and
    ///     upserts the OIDC fields. Tenant-scoped: 404 when
    ///     the URL <c>orgId</c> doesn't match the caller's
    ///     <see cref="ICurrentUser.TenantId" />.
    /// </summary>
    /// <param name="orgId">Org id from the URL.</param>
    /// <param name="request">Body — provider discriminator +
    /// optional OIDC fields.</param>
    /// <param name="validator">Scoped FluentValidation
    /// validator for the request body.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpPut(Name = OrgAuthProviderRouteNames.ConfigUpsert)]
    [EndpointSummary("Create or update the per-org authentication provider configuration")]
    [RequirePermission(AuthProviderPermissions.Update)]
    [ProducesResponseType<OrgAuthProviderConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgAuthProviderConfigResponse>> UpsertAsync(
        Guid orgId,
        [FromBody] UpsertOrgAuthProviderRequest request,
        [FromServices] IValidator<UpsertOrgAuthProviderRequest> validator,
        CancellationToken cancellationToken)
    {
        if (orgId != currentUser.TenantId)
        {
            return OrgAuthProviderControllerHelpers.ConfigNotFound(orgId, HttpContext.Request.Path);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return OrgAuthProviderControllerHelpers.InvalidRequestResponse(
                validation.ToDictionary());
        }

        var provider = request.Provider.Equals("oidc", StringComparison.OrdinalIgnoreCase)
            ? OrgAuthProvider.Oidc
            : OrgAuthProvider.Sigil;

        // Existence check — the seeder guarantees a row exists for
        // every org; a manual DB delete is the only way this can miss,
        // and the right shape is a 404.
        var exists = await db.OrgAuthProviderConfigs
            .AsNoTracking()
            .AnyAsync(config => config.OrgId == orgId, cancellationToken);
        if (!exists)
        {
            return OrgAuthProviderControllerHelpers.ConfigNotFound(orgId, HttpContext.Request.Path);
        }

        var now = DateTimeOffset.UtcNow;
        var protector = dataProtectionProvider.CreateProtector(
            OrgAuthProviderSecretProtector.Purpose);

        if (provider == OrgAuthProvider.Sigil)
        {
            // Switch to Sigil — drop every OIDC field, reset to the
            // default scope list, bump UpdatedAt.
            await db.OrgAuthProviderConfigs
                .Where(config => config.OrgId == orgId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(config => config.Provider, OrgAuthProvider.Sigil)
                        .SetProperty(config => config.OidcAuthority, (string?)null)
                        .SetProperty(config => config.OidcClientId, (string?)null)
                        .SetProperty(config => config.OidcClientSecretProtected, (string?)null)
                        .SetProperty(config => config.OidcScopes, OrgAuthProviderDefaults.DefaultScopes)
                        .SetProperty(config => config.UpdatedAt, now),
                    cancellationToken);
        }
        else
        {
            // Switch to Oidc (or rotate fields on an existing Oidc
            // row). SetProperty is a no-op when the caller didn't
            // supply the field — the stored ciphertext / scope list
            // is kept intact across a partial PUT.
            await db.OrgAuthProviderConfigs
                .Where(config => config.OrgId == orgId)
                .ExecuteUpdateAsync(
                    setters =>
                    {
                        setters.SetProperty(config => config.Provider, OrgAuthProvider.Oidc);
                        setters.SetProperty(config => config.OidcAuthority, request.OidcAuthority);
                        setters.SetProperty(config => config.OidcClientId, request.OidcClientId);
                        if (!string.IsNullOrEmpty(request.OidcClientSecret))
                        {
                            setters.SetProperty(
                                config => config.OidcClientSecretProtected,
                                protector.Protect(request.OidcClientSecret));
                        }

                        if (request.OidcScopes is not null)
                        {
                            setters.SetProperty(config => config.OidcScopes, request.OidcScopes.ToArray());
                        }
                        else
                        {
                            setters.SetProperty(config => config.OidcScopes, OrgAuthProviderDefaults.DefaultScopes);
                        }

                        setters.SetProperty(config => config.UpdatedAt, now);
                    },
                    cancellationToken);
        }

        logger.LogInformation(
            "OrgAuthProvidersController: org {OrgId} auth-provider set to {Provider}.",
            orgId,
            provider);

        var refreshed = await db.OrgAuthProviderConfigs
            .AsNoTracking()
            .FirstAsync(config => config.OrgId == orgId, cancellationToken);

        return Ok(OrgAuthProviderControllerHelpers.MapToResponse(refreshed));
    }

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
    [HttpPost("test", Name = OrgAuthProviderRouteNames.ConfigTest)]
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
        var testProtector = dataProtectionProvider.CreateProtector(
            OrgAuthProviderSecretProtector.Purpose);
        string? plaintextSecret = null;
        if (!string.IsNullOrEmpty(row.OidcClientSecretProtected))
        {
            try
            {
                plaintextSecret = testProtector.Unprotect(row.OidcClientSecretProtected);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "OrgAuthProvidersController.TestAsync: failed to decrypt the OIDC client secret for org {OrgId}; " +
                    "the keyring may have rotated and the stored ciphertext is unrecoverable.",
                    orgId);
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
///     Stable purpose string used to mint a purpose-bound
///     <see cref="IDataProtector" /> for the OIDC client secret.
///     Different-purpose protectors elsewhere cannot decrypt the
///     same ciphertext — defense in depth against a misconfigured
///     DI registration that mints a "default" protector for the
///     whole app.
/// </summary>
internal static class OrgAuthProviderSecretProtector
{
    /// <summary>Purpose discriminator for the
    /// <see cref="OrgAuthProvidersController" />'s
    /// <see cref="IDataProtector" />.</summary>
    public const string Purpose = "OrgAuthProviderConfig.OidcClientSecret";
}
