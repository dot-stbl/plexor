// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingControllerHelpers — file-static helpers pulled out of
// BrandingController.cs to satisfy the no-private-methods convention
// (class-layout-and-tooling.md §1a / §9.4 — Controller / minimal API
// endpoint). The controller is a thin orchestration layer; the
// mapping, ProblemDetails construction, and tenant-scoped cross-tenant
// check live here.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Branding.Api.Models;
using Plexor.Modules.Branding.Domain.Entities;

namespace Plexor.Modules.Branding.Api.Controllers;

/// <summary>
///     Helpers for <see cref="BrandingController" />. Pure functions
///     over DTOs + entities — extracted to satisfy the no-private-methods
///     rule.
/// </summary>
internal static class BrandingControllerHelpers
{
    /// <summary>
    ///     True when the supplied <paramref name="orgId" /> doesn't
    ///     match the caller's <paramref name="callerTenantId" /> AND
    ///     the caller isn't an unauthenticated empty-Guid. Empty
    ///     <paramref name="callerTenantId" /> (anonymous) bypasses the
    ///     check — the boot endpoint resolves to operator defaults
    ///     only.
    /// </summary>
    /// <param name="orgId">OrgId from the route / body.</param>
    /// <param name="callerTenantId">Caller tenant id (may be
    /// <see cref="Guid.Empty" /> for anonymous).</param>
    public static bool IsCrossTenant(Guid orgId, Guid callerTenantId)
    {
        return callerTenantId != Guid.Empty && orgId != callerTenantId;
    }

    /// <summary>
    ///     403 ProblemDetails for the cross-tenant access case.
    ///     Caller-friendly message that doesn't leak whether the
    ///     target org actually has an override row.
    /// </summary>
    /// <param name="orgId">OrgId the caller tried to access.</param>
    public static ObjectResult CrossTenantForbidden(Guid orgId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Cross-tenant access denied",
            Detail = $"The caller's tenant may not access org '{orgId}'.",
        };
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }

    /// <summary>
    ///     404 ProblemDetails for the case where the per-org override
    ///     row doesn't exist. Caller falls back to the operator
    ///     defaults.
    /// </summary>
    /// <param name="orgId">OrgId the caller queried.</param>
    public static NotFoundObjectResult OrgOverrideNotFound(Guid orgId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Org override not found",
            Detail = $"No branding override for org '{orgId}'; falling back to operator defaults.",
        };
        return new NotFoundObjectResult(problem);
    }

    /// <summary>
    ///     400 <see cref="ValidationProblemDetails" /> for the PUT
    ///     endpoints when the FluentValidation chain rejects the body.
    ///     The dictionary comes from
    ///     <c>ValidationResult.ToDictionary()</c> (property-name →
    ///     error messages); ASP.NET Core binds it into the standard
    ///     <c>errors</c> shape per RFC 9457.
    /// </summary>
    /// <param name="errors">Dictionary keyed by property name,
    /// value = the error message(s) from the validator.</param>
    public static BadRequestObjectResult InvalidRequestResponse(
        IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more request fields failed validation.",
        };
        return new BadRequestObjectResult(problem);
    }

    /// <summary>
    ///     Map a domain <see cref="GlobalThemeConfig" /> to its wire
    ///     <see cref="GlobalThemeConfigResponse" /> projection.
    /// </summary>
    /// <param name="row">EF read or upsert result.</param>
    public static GlobalThemeConfigResponse ToGlobalResponse(GlobalThemeConfig row)
    {
        return new GlobalThemeConfigResponse
        {
            BrandName = row.BrandName,
            BrandLogoUrl = row.BrandLogoUrl,
            BrandFaviconUrl = row.BrandFaviconUrl,
            DefaultPresetId = row.DefaultPresetId,
            CustomAccent = row.CustomAccent,
            UpdatedAt = row.UpdatedAt,
        };
    }

    /// <summary>
    ///     Map a domain <see cref="OrgThemeConfig" /> to its wire
    ///     <see cref="OrgThemeConfigResponse" /> projection.
    /// </summary>
    /// <param name="row">EF read or upsert result.</param>
    public static OrgThemeConfigResponse ToOrgResponse(OrgThemeConfig row)
    {
        return new OrgThemeConfigResponse
        {
            OrgId = row.OrgId,
            PresetId = row.PresetId,
            CustomAccent = row.CustomAccent,
            BrandName = row.BrandName,
            BrandLogoUrl = row.BrandLogoUrl,
            BrandFaviconUrl = row.BrandFaviconUrl,
            UpdatedAt = row.UpdatedAt,
        };
    }

    /// <summary>
    ///     Map an <see cref="UpsertGlobalThemeConfigRequest" /> body to
    ///     its domain <see cref="GlobalThemeConfig" />. The
    ///     <see cref="GlobalThemeConfig.Id" /> / <see cref="GlobalThemeConfig.UpdatedAt" />
    ///     / <see cref="GlobalThemeConfig.UpdatedBy" /> are populated by the
    ///     service (read existing + apply + SaveChanges).
    /// </summary>
    /// <param name="request">Wire shape.</param>
    public static GlobalThemeConfig ToEntity(UpsertGlobalThemeConfigRequest request)
    {
        return new GlobalThemeConfig
        {
            BrandName = request.BrandName,
            BrandLogoUrl = request.BrandLogoUrl,
            BrandFaviconUrl = request.BrandFaviconUrl,
            DefaultPresetId = request.DefaultPresetId,
            CustomAccent = request.CustomAccent,
        };
    }

    /// <summary>
    ///     Map an <see cref="UpsertOrgThemeConfigRequest" /> body to
    ///     its domain <see cref="OrgThemeConfig" />. The
    ///     <see cref="OrgThemeConfig.OrgId" /> comes from the path
    ///     parameter (authoritative); the body's
    ///     <see cref="UpsertOrgThemeConfigRequest.OrgId" /> is
    ///     ignored.
    /// </summary>
    /// <param name="request">Wire shape.</param>
    public static OrgThemeConfig ToEntity(UpsertOrgThemeConfigRequest request)
    {
        return new OrgThemeConfig
        {
            PresetId = request.PresetId,
            CustomAccent = request.CustomAccent,
            BrandName = request.BrandName,
            BrandLogoUrl = request.BrandLogoUrl,
            BrandFaviconUrl = request.BrandFaviconUrl,
        };
    }
}