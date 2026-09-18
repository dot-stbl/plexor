// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeInstallationsControllerHelpers — file-static helpers
// pulled out of ThemeInstallationsController.cs to satisfy the
// no-private-methods convention (class-layout-and-tooling.md
// §1a / §9.4 — Controller). The controller is a thin
// orchestration layer; the mapping + ProblemDetails
// construction lives here.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Branding.Api.Models.Requests;
using Plexor.Modules.Branding.Api.Models.Responses;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Branding;

namespace Plexor.Modules.Branding.Api.Controllers;

/// <summary>
///     Helpers for <see cref="ThemeInstallationsController" />.
///     Pure functions over DTOs + the domain entity +
///     registry — extracted to satisfy the no-private-methods
///     rule.
/// </summary>
internal static class ThemeInstallationsControllerHelpers
{
    /// <summary>
    ///     404 ProblemDetails for the case where no marketplace
    ///     installation row exists for the org. Caller falls
    ///     back to the operator defaults.
    /// </summary>
    /// <param name="orgId">Org whose marketplace read missed.</param>
    public static NotFoundObjectResult NotInstalled(Guid orgId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Theme installation not found",
            Detail = $"No marketplace theme installed for org '{orgId}'; falling back to operator defaults.",
        };
        return new NotFoundObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>
    ///     400 ProblemDetails for the case where the manifest
    ///     signature doesn't match the canonical manifest bytes.
    ///     Stable <c>code</c> extension so clients can branch on
    ///     <c>code</c> rather than the human Detail string.
    /// </summary>
    /// <param name="reason">Human-readable cause from the
    /// verifier.</param>
    public static BadRequestObjectResult InvalidSignature(string reason)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Manifest signature invalid",
            Detail = reason,
            Extensions = { ["code"] = "branding.manifest.invalid_signature" },
        };
        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>
    ///     404 ProblemDetails for the case where the supplied
    ///     <c>themeId</c> doesn't match any entry in the host-side
    ///     <see cref="CommunityThemeRegistry" />.
    /// </summary>
    /// <param name="themeId">The unknown id, as supplied by the
    /// caller.</param>
    public static NotFoundObjectResult UnknownTheme(string themeId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Unknown marketplace theme",
            Detail = $"The marketplace theme id '{themeId}' is not registered on this host.",
        };
        return new NotFoundObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>
    ///     400 <see cref="ValidationProblemDetails" /> for the
    ///     PUT endpoint when the FluentValidation chain rejects
    ///     the body.
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
        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>
    ///     Map an <see cref="UpsertThemeInstallationRequest" /> to
    ///     the application-layer <see cref="ThemeManifest" />
    ///     record. The token dictionary is converted to a
    ///     case-sensitive string map — token names are
    ///     canonicalised by the FE publisher.
    /// </summary>
    /// <param name="request">Wire shape.</param>
    public static ThemeManifest ToEntity(UpsertThemeInstallationRequest request)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in request.Manifest.TokenValues)
        {
            tokens[kvp.Key] = kvp.Value;
        }

        return new ThemeManifest(
            ThemeId: request.Manifest.ThemeId,
            Name: request.Manifest.Name,
            Version: request.Manifest.Version,
            Author: request.Manifest.Author,
            TokenValues: tokens);
    }

    /// <summary>
    ///     Map a domain <see cref="ThemeInstallation" /> row to its
    ///     wire <see cref="ThemeInstallationResponse" />
    ///     projection. The <c>Name</c> / <c>Version</c> /
    ///     <c>Author</c> are looked up from the registry — the
    ///     row only stores the id + signature, not the publisher
    ///     metadata.
    /// </summary>
    /// <param name="row">EF read or upsert result.</param>
    /// <param name="registry">Host-side community-theme registry.</param>
    public static ThemeInstallationResponse ToResponse(
        ThemeInstallation row,
        CommunityThemeRegistry registry)
    {
        var theme = registry.TryFind(row.ThemeId);

        return new ThemeInstallationResponse
        {
            OrgId = row.OrgId,
            ThemeId = row.ThemeId,
            Name = theme?.Name ?? string.Empty,
            Version = theme?.Version ?? string.Empty,
            Author = theme?.Author ?? string.Empty,
            ManifestSignature = row.ManifestSignature,
            ActivatedAt = row.ActivatedAt,
            ActivatedBy = row.ActivatedBy,
        };
    }
}
