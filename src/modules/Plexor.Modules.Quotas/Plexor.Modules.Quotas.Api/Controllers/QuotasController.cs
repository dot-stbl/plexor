// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotasController — REST endpoints for the quotas capability (4.5.g.2).
// Mounts four GET endpoints under /api/v1/quotas: definitions (catalog),
// assignments (per-scope), usage (per-scope, with effective limit), and
// effective (per-definition resolved value).
//
// Write endpoints (PUT / DELETE on assignments) land in 4.5.g.3.
//
// All endpoints are gated by [RequirePermission(QuotaPermissions.Read)].
// Tenant scoping is enforced at the query level by passing
// currentUser.TenantId as the orgId filter to the repositories.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Quotas.Api.Models;
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Api.Controllers;

/// <summary>
///     Stable route names referenced by <c>[HttpGet(..., Name = ...)]</c>
///     and the OpenAPI document generator. File-scoped so the constants
///     stay local to the file that owns them (constructors-and-fields.md).
/// </summary>
file static class QuotasRouteNames
{
    /// <summary><c>GET /api/v1/quotas/definitions</c> — list the catalog.</summary>
    public const string DefinitionsList = "quotas-definitions-list";

    /// <summary><c>GET /api/v1/quotas/assignments</c> — list assignments at one scope.</summary>
    public const string AssignmentsList = "quotas-assignments-list";

    /// <summary><c>GET /api/v1/quotas/usage</c> — list current usage at one scope.</summary>
    public const string UsageList = "quotas-usage-list";

    /// <summary><c>GET /api/v1/quotas/effective</c> — list resolved effective values.</summary>
    public const string EffectiveList = "quotas-effective-list";

    /// <summary><c>PUT /api/v1/quotas/assignments</c> — upsert one assignment.</summary>
    public const string AssignmentsUpsert = "quotas-assignments-upsert";

    /// <summary><c>DELETE /api/v1/quotas/assignments/{assignmentId}</c> — remove one assignment.</summary>
    public const string AssignmentsDelete = "quotas-assignments-delete";
}

/// <summary>
///     Read endpoints for the quotas capability. Mounted at
///     <c>/api/v1/quotas/*</c> via <see cref="ApiRoutes.Base" />.
///     All four endpoints require the <c>quotas.read</c> permission
///     (admins with the <c>*</c> wildcard pass through; the
///     <c>viewer</c> role seeded by the Migrator's
///     <c>IdentityBootstrapper</c> also has <c>quotas.read</c>).
/// </summary>
/// <param name="catalog">Scoped <see cref="IQuotaCatalog" /> —
/// reads the catalog rows.</param>
/// <param name="assignments">Scoped <see cref="IQuotaAssignmentRepository" /> —
/// reads QuotaAssignment rows at the requested scope + org.</param>
/// <param name="usage">Scoped <see cref="IQuotaUsageReader" /> —
/// reads the latest consumption snapshots at the requested scope + org.</param>
/// <param name="resolver">Scoped <see cref="IQuotaScopeResolver" /> —
/// resolves the effective limit via the folder → org → default walk.</param>
/// <param name="currentUser">Scoped <see cref="ICurrentUser" /> —
/// supplies the caller's <c>TenantId</c> for tenant-scoped queries.</param>
[ApiController]
[Route($"{ApiRoutes.Base}/quotas")]
[Tags(["quotas"])]
[Authorize]
public sealed class QuotasController(
    IQuotaCatalog catalog,
    IQuotaAssignmentRepository assignments,
    IQuotaUsageReader usage,
    IQuotaScopeResolver resolver,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    ///     <c>GET /api/v1/quotas/definitions</c> — list every catalog
    ///     entry. Global (no tenant scoping; the catalog is shared).
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet("definitions", Name = QuotasRouteNames.DefinitionsList)]
    [EndpointSummary("List the built-in quota catalog")]
    [RequirePermission(QuotaPermissions.Read)]
    [ProducesResponseType<IReadOnlyList<QuotaDefinitionSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuotaDefinitionSummary>>> ListDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        var definitions = await catalog.ListAllAsync(cancellationToken);
        var result = definitions
            .Select(static definition => new QuotaDefinitionSummary
            {
                Id = definition.Id,
                Key = definition.Key,
                Description = definition.Description,
                Unit = definition.Unit.ToString(),
                Period = definition.Period.ToString(),
                DefaultValue = definition.DefaultValue,
            })
            .ToList();
        return Ok(result);
    }

    /// <summary>
    ///     <c>GET /api/v1/quotas/assignments?scope=org|team|folder&amp;id=X</c>
    ///     — list every assignment at the requested scope for the
    ///     caller's tenant. Returns an empty list when no rows match.
    /// </summary>
    /// <param name="scope">Scope discriminator — <c>"org"</c>,
    /// <c>"team"</c>, or <c>"folder"</c>.</param>
    /// <param name="id">Id of the matching Realm entity.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet("assignments", Name = QuotasRouteNames.AssignmentsList)]
    [EndpointSummary("List quota assignments for one scope")]
    [RequirePermission(QuotaPermissions.Read)]
    [ProducesResponseType<IReadOnlyList<QuotaAssignmentSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<QuotaAssignmentSummary>>> ListAssignmentsAsync(
        [FromQuery] string scope,
        [FromQuery] Guid id,
        CancellationToken cancellationToken)
    {
        if (!QuotasControllerHelpers.TryParseScope(scope, out var kind))
        {
            return QuotasControllerHelpers.InvalidScopeProblem(scope);
        }

        var rows = await assignments.ListForScopeAsync(
            kind,
            id,
            currentUser.TenantId,
            cancellationToken);

        var definitions = await catalog.ListAllAsync(cancellationToken);
        var keyById = QuotasControllerHelpers.BuildDefinitionKeyMap(definitions);

        var result = rows
            .Select(row => QuotasControllerHelpers.MapToSummary(row, keyById))
            .ToList();
        return Ok(result);
    }

    /// <summary>
    ///     <c>GET /api/v1/quotas/usage?scope=org|team|folder&amp;id=X</c>
    ///     — read the latest consumption snapshots at the requested
    ///     scope, paired with the resolved effective limit and a
    ///     threshold percentage when usage crosses the 80% warning line.
    /// </summary>
    /// <param name="scope">Scope discriminator — <c>"org"</c>,
    /// <c>"team"</c>, or <c>"folder"</c>.</param>
    /// <param name="id">Id of the matching Realm entity.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet("usage", Name = QuotasRouteNames.UsageList)]
    [EndpointSummary("Read current quota usage for one scope")]
    [RequirePermission(QuotaPermissions.Read)]
    [ProducesResponseType<IReadOnlyList<QuotaUsageEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<QuotaUsageEntry>>> GetUsageAsync(
        [FromQuery] string scope,
        [FromQuery] Guid id,
        CancellationToken cancellationToken)
    {
        if (!QuotasControllerHelpers.TryParseScope(scope, out var kind))
        {
            return QuotasControllerHelpers.InvalidScopeProblem(scope);
        }

        var rows = await usage.ListForScopeAsync(
            kind,
            id,
            currentUser.TenantId,
            cancellationToken);

        var definitions = await catalog.ListAllAsync(cancellationToken);
        var keyById = QuotasControllerHelpers.BuildDefinitionKeyMap(definitions);

        var result = await QuotasControllerHelpers.BuildUsageEntriesAsync(
            rows,
            keyById,
            new QuotaScope(kind, id, currentUser.TenantId),
            resolver,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    ///     <c>GET /api/v1/quotas/effective?scope=org|team|folder&amp;id=X</c>
    ///     — one row per catalog key, holding the resolved effective
    ///     value and the origin scope that supplied it. The dashboard
    ///     uses this to render the "current limits" panel without
    ///     repeating the resolver walk per request.
    /// </summary>
    /// <param name="scope">Scope discriminator — <c>"org"</c>,
    /// <c>"team"</c>, or <c>"folder"</c>.</param>
    /// <param name="id">Id of the matching Realm entity.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpGet("effective", Name = QuotasRouteNames.EffectiveList)]
    [EndpointSummary("Read the resolved effective quota values for one scope")]
    [RequirePermission(QuotaPermissions.Read)]
    [ProducesResponseType<IReadOnlyList<EffectiveQuotaEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EffectiveQuotaEntry>>> GetEffectiveAsync(
        [FromQuery] string scope,
        [FromQuery] Guid id,
        CancellationToken cancellationToken)
    {
        if (!QuotasControllerHelpers.TryParseScope(scope, out var kind))
        {
            return QuotasControllerHelpers.InvalidScopeProblem(scope);
        }

        var definitions = await catalog.ListAllAsync(cancellationToken);
        var effectiveScope = new QuotaScope(kind, id, currentUser.TenantId);

        var entries = await QuotasControllerHelpers.BuildEffectiveEntriesAsync(
            definitions,
            effectiveScope,
            resolver,
            cancellationToken);

        return Ok(entries);
    }

    /// <summary>
    ///     <c>PUT /api/v1/quotas/assignments</c> — create or update one
    ///     quota assignment at the requested scope. Tenant-scoped: the
    ///     assignment's <c>OrgId</c> is set from
    ///     <see cref="ICurrentUser.TenantId" />, so callers in Org X
    ///     cannot create or modify Org Y's rows. The body is validated
    ///     by <see cref="Api.Validation.UpsertQuotaAssignmentRequestValidator" />
    ///     before any catalog lookup.
    /// </summary>
    /// <param name="request">Body — catalog key, scope, value, optional
    /// period.</param>
    /// <param name="validator">Scoped <see cref="IValidator{T}" /> for
    /// the request body.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpPut("assignments", Name = QuotasRouteNames.AssignmentsUpsert)]
    [EndpointSummary("Create or update a quota assignment at the org scope")]
    [RequirePermission(QuotaPermissions.AssignOrg)]
    [ProducesResponseType<QuotaAssignmentSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotaAssignmentSummary>> UpsertAssignmentAsync(
        [FromBody] UpsertQuotaAssignmentRequest request,
        [FromServices] IValidator<UpsertQuotaAssignmentRequest> validator,
        CancellationToken cancellationToken)
    {

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return QuotasControllerHelpers.InvalidRequestResponse(
                validation.ToDictionary());
        }

        if (!QuotasControllerHelpers.TryParseScope(request.ScopeKind, out var kind))
        {

            // Should be unreachable: the validator already rejected any
            // ScopeKind outside {org, team, folder}. Defensive against
            // a future validator change.
            return QuotasControllerHelpers.InvalidScopeProblem(request.ScopeKind);
        }

        var definition = await catalog.FindByKeyAsync(request.DefinitionKey, cancellationToken);
        if (definition is null)
        {
            return Problem(
                detail: $"No QuotaDefinition with key '{request.DefinitionKey}'.",
                instance: HttpContext.Request.Path,
                statusCode: StatusCodes.Status404NotFound,
                title: "Unknown quota definition");
        }

        var period = string.IsNullOrEmpty(request.Period)
            ? definition.Period
            : Enum.Parse<QuotaPeriod>(request.Period);

        var scope = new QuotaScope(kind, request.ScopeId, currentUser.TenantId);
        var row = await assignments.UpsertAsync(
            definition.Id,
            scope,
            request.Value,
            period,
            currentUser.UserId,
            cancellationToken);

        return Ok(QuotasControllerHelpers.MapToSummary(row, definition));
    }

    /// <summary>
    ///     <c>DELETE /api/v1/quotas/assignments/{assignmentId}</c> —
    ///     remove one assignment. Tenant-scoped: a row whose
    ///     <c>OrgId</c> doesn't match the caller's
    ///     <see cref="ICurrentUser.TenantId" /> returns 404 — same
    ///     shape as a missing id, so callers can't probe another
    ///     org's assignment ids.
    /// </summary>
    /// <param name="assignmentId">UUID v7 of the assignment row.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpDelete("assignments/{assignmentId:guid}", Name = QuotasRouteNames.AssignmentsDelete)]
    [RequirePermission(QuotaPermissions.AssignOrg)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var existing = await assignments.FindAsync(assignmentId, cancellationToken);
        if (existing is null || existing.OrgId != currentUser.TenantId)
        {
            return Problem(
                detail: $"No assignment '{assignmentId}' in the caller's org.",
                instance: HttpContext.Request.Path,
                statusCode: StatusCodes.Status404NotFound,
                title: "Assignment not found");
        }

        await assignments.DeleteAsync(assignmentId, cancellationToken);
        return NoContent();
    }
}
