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
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Branding;
using Plexor.Shared.Kernel.Audit;

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

    /// <summary>
    ///     Compose the <c>theme_installed.activated</c> audit
    ///     payload from the freshly-upserted
    ///     <see cref="ThemeInstallation" /> row and emit it
    ///     through <paramref name="auditEmitter" />.
    ///     <see cref="AuditActions.ThemeInstalledActivated" />'s
    ///     documented payload keys (<c>theme_id</c>,
    ///     <c>signature</c>) are what the admin UI timeline
    ///     needs to render the activation — the full hex
    ///     signature is carried so a future audit-trail viewer
    ///     can re-verify the manifest against the publisher key.
    /// </summary>
    /// <param name="auditEmitter">
    ///     Scoped <see cref="IAuditEmitter" /> resolved from the
    ///     request scope. Fire-and-forget — emits never throw.
    /// </param>
    /// <param name="row">
    ///     The freshly-upserted installation row returned by
    ///     <c>IThemeInstallationService.UpsertAsync</c>.
    /// </param>
    /// <param name="actorUserId">
    ///     Id of the user that triggered the PUT
    ///     (<c>ICurrentUser.UserId</c>).
    /// </param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    /// A completed <see cref="Task" />. The IAuditEmitter
    /// contract swallows emit failures.
    /// </returns>
    public static Task EmitThemeActivatedAsync(
        IAuditEmitter auditEmitter,
        ThemeInstallation row,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        // Phase 5+ wire name — stable dot.case per AuditActions.
        // The signature is informational (a re-verification handle
        // for the admin UI), not a credential. The full 64-char
        // hex HMAC is carried in the payload so a future admin UI
        // can re-verify it against the publisher key without
        // rejoining the row.
        var payload = new Dictionary<string, object?>
        {
            ["theme_id"] = row.ThemeId,
            ["signature"] = row.ManifestSignature,
        };

        return auditEmitter.EmitAsync(
            AuditActions.ThemeInstalledActivated,
            new AuditContext(
                OrgId: row.OrgId,
                ActorUserId: actorUserId,
                TargetKind: "theme_installation",
                TargetId: row.OrgId,
                Payload: payload),
            cancellationToken);
    }

    /// <summary>
    ///     Compose the <c>theme_installed.deactivated</c> audit
    ///     payload from the row that was just removed by
    ///     <c>IThemeInstallationService.DeleteAsync</c> and emit
    ///     it through <paramref name="auditEmitter" />.
    ///     <see cref="AuditActions.ThemeInstalledDeactivated" />'s
    ///     documented payload key (<c>theme_id</c>) is what the
    ///     admin UI timeline needs to render the deactivation.
    /// </summary>
    /// <param name="auditEmitter">
    ///     Scoped <see cref="IAuditEmitter" /> resolved from the
    ///     request scope. Fire-and-forget — emits never throw.
    /// </param>
    /// <param name="orgId">
    ///     Tenant the row belonged to. Passed explicitly so the
    ///     payload's <c>OrgId</c> is populated even though the
    ///     underlying row has been deleted by the time we get
    ///     here.
    /// </param>
    /// <param name="themeId">
    ///     Marketplace id of the theme that was active before
    ///     the DELETE — captured by the controller via a
    ///     pre-delete <c>GetForOrgAsync</c> read (the service's
    ///     DeleteAsync only returns a boolean, so the theme id
    ///     has to be sourced separately).
    /// </param>
    /// <param name="actorUserId">
    ///     Id of the user that triggered the DELETE
    ///     (<c>ICurrentUser.UserId</c>).
    /// </param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    /// A completed <see cref="Task" />. The IAuditEmitter
    /// contract swallows emit failures.
    /// </returns>
    public static Task EmitThemeDeactivatedAsync(
        IAuditEmitter auditEmitter,
        Guid orgId,
        string themeId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["theme_id"] = themeId,
        };

        return auditEmitter.EmitAsync(
            AuditActions.ThemeInstalledDeactivated,
            new AuditContext(
                OrgId: orgId,
                ActorUserId: actorUserId,
                TargetKind: "theme_installation",
                TargetId: orgId,
                Payload: payload),
            cancellationToken);
    }
}
