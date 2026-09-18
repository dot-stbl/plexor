// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingController — REST endpoints for the branding capability.
// Mounts six endpoints under /api/v1/branding: global get/upsert,
// per-org get/upsert/delete, and the boot-resolution endpoint the
// FE boot script consumes.
//
// Read endpoints require branding.read; write endpoints require
// branding.update. Tenant scoping on the per-org endpoints is
// enforced at the controller layer (the supplied orgId must match
// the caller's currentUser.TenantId) — cross-tenant callers get a
// 403, not a 404, because the row genuinely exists in a different
// org and silently returning a 404 would hide the access attempt
// from the audit log.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Branding.Api.Models.Requests;
using Plexor.Modules.Branding.Api.Models.Responses;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.Branding;
using Plexor.Shared.Kernel.Identity;

namespace Plexor.Modules.Branding.Api.Controllers;

/// <summary>
///     Stable route names referenced by <c>[HttpGet/Post/Put/Delete(..., Name = ...)]</c>.
/// </summary>
file static class BrandingRouteNames
{
    /// <summary>GET /api/v1/branding/global — read the operator-global row.</summary>
    public const string GlobalGet = "branding-global-get";

    /// <summary>PUT /api/v1/branding/global — upsert the operator-global row.</summary>
    public const string GlobalUpsert = "branding-global-upsert";

    /// <summary>GET /api/v1/branding/org/{orgId} — read the per-org override row.</summary>
    public const string OrgGet = "branding-org-get";

    /// <summary>PUT /api/v1/branding/org/{orgId} — upsert the per-org override row.</summary>
    public const string OrgUpsert = "branding-org-upsert";

    /// <summary>DELETE /api/v1/branding/org/{orgId} — delete the per-org override row (reset to inherit).</summary>
    public const string OrgDelete = "branding-org-delete";

    /// <summary>GET /api/v1/branding/boot — resolved boot config the FE consumes.</summary>
    public const string BootGet = "branding-boot-get";
}

/// <summary>
///     Branding endpoints. Mounted at <c>/api/v1/branding/*</c> via
///     <see cref="ApiRoutes.Base" />. Read endpoints require
///     <c>branding.read</c>; PUT / DELETE require
///     <c>branding.update</c>. Per-org endpoints scope to the
///     caller's <see cref="ICurrentUser.TenantId" />.
/// </summary>
/// <param name="service">Scoped <see cref="IBrandingService" /> —
/// reads + writes the operator-global + per-org rows and resolves
/// the merged boot config.</param>
/// <param name="currentUser">Scoped <see cref="ICurrentUser" /> —
/// supplies the caller's <c>TenantId</c> + <c>UserId</c> for
/// tenant-scoped writes and audit context.</param>
[ApiController]
[Route($"{ApiRoutes.Base}/branding")]
[Tags(["branding"])]
[Authorize]
public sealed class BrandingController(
    IBrandingService service,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    ///     <c>GET /api/v1/branding/global</c> — read the
    ///     operator-global branding row. The seeder inserts the
    ///     singleton row on first boot; the controller returns the
    ///     documented defaults when the row doesn't exist yet (defensive
    ///     against a race on a fresh install).
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet("global", Name = BrandingRouteNames.GlobalGet)]
    [EndpointSummary("Read the operator-global branding row")]
    [RequirePermission(BrandingPermissions.Read)]
    [ProducesResponseType<GlobalThemeConfigResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GlobalThemeConfigResponse>> GetGlobalAsync(
        CancellationToken cancellationToken)
    {
        var row = await service.GetGlobalAsync(cancellationToken);
        return Ok(BrandingControllerHelpers.ToGlobalResponse(row));
    }

    /// <summary>
    ///     <c>PUT /api/v1/branding/global</c> — upsert the
    ///     operator-global branding row. Tenant-scoped: callers in
    ///     Org X cannot mutate the global row (only the operator's
    ///     admin role with the wildcard permission can).
    /// </summary>
    /// <param name="request">Body — operator branding fields.</param>
    /// <param name="validator">Scoped FluentValidation validator for
    /// the request body.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpPut("global", Name = BrandingRouteNames.GlobalUpsert)]
    [EndpointSummary("Upsert the operator-global branding row")]
    [RequirePermission(BrandingPermissions.Update)]
    [ProducesResponseType<GlobalThemeConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GlobalThemeConfigResponse>> UpsertGlobalAsync(
        [FromBody] UpsertGlobalThemeConfigRequest request,
        [FromServices] IValidator<UpsertGlobalThemeConfigRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BrandingControllerHelpers.InvalidRequestResponse(
                validation.ToDictionary());
        }

        var row = await service.UpsertGlobalAsync(
            BrandingControllerHelpers.ToEntity(request),
            currentUser.UserId,
            cancellationToken);
        return Ok(BrandingControllerHelpers.ToGlobalResponse(row));
    }

    /// <summary>
    ///     <c>GET /api/v1/branding/org/{orgId}</c> — read the per-org
    ///     override row. Returns 404 when no override row exists
    ///     (caller falls back to the operator defaults). Tenant
    ///     scoping: a caller from Org X cannot read Org Y's override
    ///     (403).
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet("org/{orgId:guid}", Name = BrandingRouteNames.OrgGet)]
    [EndpointSummary("Read the per-org branding override row")]
    [RequirePermission(BrandingPermissions.Read)]
    [ProducesResponseType<OrgThemeConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgThemeConfigResponse>> GetOrgAsync(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        if (BrandingControllerHelpers.IsCrossTenant(orgId, currentUser.TenantId))
        {
            return BrandingControllerHelpers.CrossTenantForbidden(orgId);
        }

        var row = await service.GetOrgOverrideAsync(orgId, cancellationToken);
        if (row is null)
        {
            return BrandingControllerHelpers.OrgOverrideNotFound(orgId);
        }

        return Ok(BrandingControllerHelpers.ToOrgResponse(row));
    }

    /// <summary>
    ///     <c>PUT /api/v1/branding/org/{orgId}</c> — upsert the per-org
    ///     override row. Tenant-scoped: a caller from Org X cannot
    ///     mutate Org Y's override (403). Idempotent — first call
    ///     inserts; subsequent calls update in place.
    /// </summary>
    /// <param name="orgId">Tenant scope (path is authoritative —
    /// body's OrgId is ignored).</param>
    /// <param name="request">Body — per-org override fields (null
    /// fields = inherit).</param>
    /// <param name="validator">Scoped FluentValidation validator for
    /// the request body.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpPut("org/{orgId:guid}", Name = BrandingRouteNames.OrgUpsert)]
    [EndpointSummary("Upsert the per-org branding override row")]
    [RequirePermission(BrandingPermissions.Update)]
    [ProducesResponseType<OrgThemeConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrgThemeConfigResponse>> UpsertOrgAsync(
        Guid orgId,
        [FromBody] UpsertOrgThemeConfigRequest request,
        [FromServices] IValidator<UpsertOrgThemeConfigRequest> validator,
        CancellationToken cancellationToken)
    {
        if (BrandingControllerHelpers.IsCrossTenant(orgId, currentUser.TenantId))
        {
            return BrandingControllerHelpers.CrossTenantForbidden(orgId);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BrandingControllerHelpers.InvalidRequestResponse(
                validation.ToDictionary());
        }

        var row = await service.UpsertOrgOverrideAsync(
            orgId,
            BrandingControllerHelpers.ToEntity(request),
            currentUser.UserId,
            cancellationToken);
        return Ok(BrandingControllerHelpers.ToOrgResponse(row));
    }

    /// <summary>
    ///     <c>DELETE /api/v1/branding/org/{orgId}</c> — reset the
    ///     per-org override. Tenant-scoped: a caller from Org X
    ///     cannot reset Org Y's override (403). Idempotent — missing
    ///     row is a 204 anyway.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpDelete("org/{orgId:guid}", Name = BrandingRouteNames.OrgDelete)]
    [EndpointSummary("Reset the per-org branding override (delete the row)")]
    [RequirePermission(BrandingPermissions.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteOrgAsync(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        if (BrandingControllerHelpers.IsCrossTenant(orgId, currentUser.TenantId))
        {
            return BrandingControllerHelpers.CrossTenantForbidden(orgId);
        }

        await service.DeleteOrgOverrideAsync(orgId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    ///     <c>GET /api/v1/branding/boot</c> — resolve the merged
    ///     boot config the FE consumes. Tenant-scoped: when the
    ///     caller is authenticated, resolves with the caller's
    ///     <see cref="ICurrentUser.TenantId" />; anonymous callers
    ///     (still Authorize'd, but no JWT) get the operator defaults
    ///     only.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet("boot", Name = BrandingRouteNames.BootGet)]
    [EndpointSummary("Read the resolved boot config for the caller's tenant (or operator defaults)")]
    [RequirePermission(BrandingPermissions.Read)]
    [ProducesResponseType<ResolvedBrandingConfig>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResolvedBrandingConfig>> GetBootAsync(
        CancellationToken cancellationToken)
    {
        Guid? orgId = currentUser.TenantId == Guid.Empty ? null : currentUser.TenantId;
        var resolved = await service.ResolveForOrgAsync(orgId, cancellationToken);
        return Ok(resolved);
    }
}