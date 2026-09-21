// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeInstallationsController — REST endpoints for the theme
// marketplace persistence layer. Three endpoints under
// /api/v1/branding/theme:
//
//   GET   — read the per-org installation row (404 when none)
//   PUT   — upsert the per-org installation row (signed manifest)
//   DELETE — reset the per-org installation (back to operator defaults)
//
// Tenant scoping mirrors the per-org branding pattern: a caller
// from Org X cannot see / mutate Org Y's installation (403).
// Cross-tenant reads are mapped to 403 (not 404) for the same
// audit-trail reason the existing BrandingController applies
// the same rule.
//
// This controller lives alongside BrandingController (rather than
// inside it) because the marketplace is a separate concern from
// the operator-global + per-org override flow — separate
// controllers keep the per-tenant override path fast and the
// installer happy with one AddApplicationPart per controller.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Branding.Api.Models.Requests;
using Plexor.Modules.Branding.Api.Models.Responses;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Infrastructure.Branding;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.Audit;
using Plexor.Shared.Kernel.Branding;
using Plexor.Shared.Kernel.Identity;

namespace Plexor.Modules.Branding.Api.Controllers;

/// <summary>
///     Stable route names referenced by the OpenAPI document
///     generator. File-scoped per constructors-and-fields.md.
/// </summary>
file static class ThemeInstallationRouteNames
{
    /// <summary>GET /api/v1/branding/theme — read the per-org
    /// marketplace installation row.</summary>
    public const string ThemeGet = "branding-theme-get";

    /// <summary>PUT /api/v1/branding/theme — upsert the per-org
    /// installation row (signed manifest).</summary>
    public const string ThemeUpsert = "branding-theme-upsert";

    /// <summary>DELETE /api/v1/branding/theme — reset the per-org
    /// installation (back to operator defaults).</summary>
    public const string ThemeDelete = "branding-theme-delete";
}

/// <summary>
///     Theme-marketplace endpoints. Mounted at
///     <c>/api/v1/branding/theme*</c> via <see cref="ApiRoutes.Base" />.
///     Read endpoint requires <c>branding.read</c>; PUT / DELETE
///     require <c>branding.theme.update</c>. Tenant-scoped: a
///     caller from Org X cannot see / mutate Org Y's installation
///     (403).
/// </summary>
/// <param name="service">Scoped <see cref="IThemeInstallationService" />
/// — reads + upserts + deletes the
/// <c>branding.theme_installations</c> row per tenant.</param>
/// <param name="registry">Singleton <see cref="CommunityThemeRegistry" />
/// — maps a <c>themeId</c> to its canonical publisher record so
/// the host doesn't have to trust a client-supplied id.</param>
/// <param name="currentUser">Scoped <see cref="ICurrentUser" />
/// — supplies the caller's <c>TenantId</c> + <c>UserId</c>.</param>
/// <param name="auditEmitter">
///     Scoped <see cref="IAuditEmitter" /> — emits the
///     <c>theme_installed.activated</c> / <c>deactivated</c>
///     audit events on every successful PUT / DELETE
///     (Phase 5+). Fire-and-forget: an emit failure is logged
///     at critical level inside the emitter and never breaks
///     the user request.</param>
[ApiController]
[Route($"{ApiRoutes.Base}/branding/theme")]
[Tags(["branding"])]
[Authorize]
public sealed class ThemeInstallationsController(
    IThemeInstallationService service,
    CommunityThemeRegistry registry,
    ICurrentUser currentUser,
    IAuditEmitter auditEmitter) : ControllerBase
{
    /// <summary>
    ///     <c>GET /api/v1/branding/theme</c> — read the per-org
    ///     marketplace installation. Tenant-scoped: a caller from
    ///     Org X cannot read Org Y's installation (403). Returns
    ///     404 when no marketplace theme has been activated yet;
    ///     the FE boot script then falls back to the resolved
    ///     operator defaults.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet(Name = ThemeInstallationRouteNames.ThemeGet)]
    [EndpointSummary("Read the active marketplace theme for the caller's tenant")]
    [RequirePermission(BrandingPermissions.Read)]
    [ProducesResponseType<ThemeInstallationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThemeInstallationResponse>> GetAsync(
        CancellationToken cancellationToken)
    {
        var orgId = currentUser.TenantId;

        var row = await service.GetForOrgAsync(orgId, cancellationToken);
        if (row is null)
        {
            return ThemeInstallationsControllerHelpers.NotInstalled(orgId);
        }

        return Ok(ThemeInstallationsControllerHelpers.ToResponse(row, registry));
    }

    /// <summary>
    ///     <c>PUT /api/v1/branding/theme</c> — upsert the per-org
    ///     installation row. The host looks the theme up in its
    ///     bundled <c>CommunityThemeRegistry</c>, signs the
    ///     canonical manifest with the purpose-bound HMAC
    ///     verifier, and persists the row. Tenant-scoped: a
    ///     caller from Org X cannot mutate Org Y's
    ///     installation (403).
    /// </summary>
    /// <param name="request">Body — theme id (publisher
    /// identity to install).</param>
    /// <param name="validator">Scoped FluentValidation validator
    /// for the request body.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpPut(Name = ThemeInstallationRouteNames.ThemeUpsert)]
    [EndpointSummary("Upsert the marketplace theme installation for the caller's tenant")]
    [RequirePermission(BrandingPermissions.ThemeUpdate)]
    [ProducesResponseType<ThemeInstallationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThemeInstallationResponse>> UpsertAsync(
        [FromBody] UpsertThemeInstallationRequest request,
        [FromServices] IValidator<UpsertThemeInstallationRequest> validator,
        CancellationToken cancellationToken)
    {
        var orgId = currentUser.TenantId;

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ThemeInstallationsControllerHelpers.InvalidRequestResponse(
                validation.ToDictionary());
        }

        try
        {
            var row = await service.UpsertAsync(
                orgId,
                request.ThemeId,
                currentUser.UserId,
                cancellationToken);

            // Phase 5+ — emit theme_installed.activated so the
            // admin audit log captures every activation per-org.
            // Fire-and-forget: EmitAsync never throws.
            await ThemeInstallationsControllerHelpers.EmitThemeActivatedAsync(
                auditEmitter,
                row,
                currentUser.UserId,
                cancellationToken);

            return Ok(ThemeInstallationsControllerHelpers.ToResponse(row, registry));
        }
        catch (UnknownThemeException exception)
        {
            return ThemeInstallationsControllerHelpers.UnknownTheme(exception.ThemeId);
        }
    }

    /// <summary>
    ///     <c>DELETE /api/v1/branding/theme</c> — reset the per-org
    ///     marketplace installation. Tenant-scoped: a caller from
    ///     Org X cannot reset Org Y's installation (403). Idempotent
    ///     — a missing row returns 204 anyway.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpDelete(Name = ThemeInstallationRouteNames.ThemeDelete)]
    [EndpointSummary("Reset the marketplace theme installation (delete the row)")]
    [RequirePermission(BrandingPermissions.ThemeUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(
        CancellationToken cancellationToken)
    {
        var orgId = currentUser.TenantId;

        // Capture the theme id BEFORE the delete — the service's
        // DeleteAsync only returns a boolean. We need the id for
        // the audit payload so the admin timeline can render
        // "deactivated X". A no-op delete (no row present) is
        // idempotent and does NOT emit — the audit trail records
        // successful deactivations, not call attempts.
        var existing = await service.GetForOrgAsync(orgId, cancellationToken);

        var removed = await service.DeleteAsync(orgId, cancellationToken);
        if (removed && existing is not null)
        {
            // Phase 5+ — emit theme_installed.deactivated so the
            // admin audit log captures every deactivation per-org.
            // Fire-and-forget: EmitAsync never throws.
            await ThemeInstallationsControllerHelpers.EmitThemeDeactivatedAsync(
                auditEmitter,
                orgId,
                existing.ThemeId,
                currentUser.UserId,
                cancellationToken);
        }

        return NoContent();
    }
}
