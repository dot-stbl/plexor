// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaAssignmentsController — write endpoints for the quota assignment
// aggregate (4.5.g.3). Mounts two endpoints under /api/v1/quotas/assignments:
//
//   PUT    /api/v1/quotas/assignments            — create or update one
//                                                  assignment at the
//                                                  requested scope.
//   DELETE /api/v1/quotas/assignments/{id}      — remove one assignment.
//
// Read endpoints (catalog + per-scope list + usage + effective) live in
// <see cref="QuotasController" /> per api-design.md §6 — the read and
// write surfaces split cleanly on the audit-emitter contract (writes
// emit AssignmentChanged / AssignmentRemoved; reads do not need it).
//
// All endpoints are gated by [RequirePermission(QuotaPermissions.AssignOrg)].
// Tenant scoping is enforced at the query level by passing
// currentUser.TenantId as the orgId filter — callers in Org X cannot
// create, modify, or delete Org Y's rows.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Quotas.Api.Models;
using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Domain;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Api.Controllers;

/// <summary>
///     Stable route names for the assignment write endpoints. The
///     read-side route names live next to <see cref="QuotasController" />.
///     File-scoped so the constants stay local to the file that owns
///     them (constructors-and-fields.md).
/// </summary>
file static class QuotaAssignmentsRouteNames
{
    /// <summary><c>PUT /api/v1/quotas/assignments</c> — upsert one assignment.</summary>
    public const string AssignmentsUpsert = "quotas-assignments-upsert";

    /// <summary><c>DELETE /api/v1/quotas/assignments/{assignmentId}</c> — remove one assignment.</summary>
    public const string AssignmentsDelete = "quotas-assignments-delete";
}

/// <summary>
///     Write endpoints for quota assignments. Mounted at
///     <c>/api/v1/quotas/assignments*</c> via
///     <see cref="ApiRoutes.Base" /> + the per-action
///     <c>[HttpPut("assignments", ...)]</c> /
///     <c>[HttpDelete("assignments/{assignmentId:guid}", ...)]</c>
///     attributes (the controller's
///     <c>[Route($"{ApiRoutes.Base}/quotas")]</c> prefix is shared with
///     <see cref="QuotasController" /> for the read surface). PUT and
///     DELETE require <c>quotas.assign.org</c>; the read surface in
///     <see cref="QuotasController" /> requires <c>quotas.read</c>.
///     Tenant-scoped: the assignment's <c>OrgId</c> is set from
///     <see cref="ICurrentUser.TenantId" />, so callers in Org X
///     cannot create, modify, or delete Org Y's rows.
/// </summary>
/// <param name="catalog">Scoped <see cref="IQuotaCatalog" /> —
/// validates the catalog key supplied in the PUT body.</param>
/// <param name="assignments">Scoped <see cref="IQuotaAssignmentRepository" /> —
/// upserts / deletes QuotaAssignment rows at the requested scope + org.</param>
/// <param name="auditEmitter">Scoped <see cref="IQuotaAuditEmitter" /> —
/// emits <c>AssignmentChanged</c> on PUT and <c>AssignmentRemoved</c> on
/// DELETE (4.5.h).</param>
/// <param name="currentUser">Scoped <see cref="ICurrentUser" /> —
/// supplies the caller's <c>TenantId</c> + <c>UserId</c> for
/// tenant-scoped writes and audit context.</param>
[ApiController]
[Route($"{ApiRoutes.Base}/quotas")]
[Tags(["quotas"])]
[Authorize]
public sealed class QuotaAssignmentsController(
    IQuotaCatalog catalog,
    IQuotaAssignmentRepository assignments,
    IQuotaAuditEmitter auditEmitter,
    ICurrentUser currentUser) : ControllerBase
{
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
    [HttpPut("assignments", Name = QuotaAssignmentsRouteNames.AssignmentsUpsert)]
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

        await auditEmitter.EmitAsync(
            QuotaAuditEvent.AssignmentChanged,
            new QuotaAuditContext(
                OrgId: currentUser.TenantId,
                ActorUserId: currentUser.UserId,
                DefinitionKey: request.DefinitionKey,
                ScopeKind: kind.ToString(),
                ScopeId: request.ScopeId,
                AssignmentId: row.Id),
            cancellationToken);

        return Ok(QuotasControllerHelpers.MapToSummary(row, definition));
    }

    /// <summary>
    ///     <c>DELETE /api/v1/quotas/assignments/{assignmentId}</c> —
    ///     remove one assignment. Tenant-scoped: a row whose
    ///     <c>OrgId</c> doesn't match the caller's
    ///     <see cref="ICurrentUser.TenantId" /> returns 404 — same
    ///     shape as a missing id, so callers can't probe another
    ///     org's assignment ids. The dictionary key of the affected
    ///     <c>QuotaDefinition</c> is resolved BEFORE delete so the
    ///     audit event can carry it (the assignment row only holds
    ///     <c>DefinitionId</c>).
    /// </summary>
    /// <param name="assignmentId">UUID v7 of the assignment row.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    [HttpDelete("assignments/{assignmentId:guid}", Name = QuotaAssignmentsRouteNames.AssignmentsDelete)]
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

        // Resolve the catalog key BEFORE delete so the audit event can
        // carry the stable wire identifier. The assignment row only
        // stores DefinitionId — the FK to quota_definitions may not be
        // preloaded, so a catalog lookup is the safe path. Missing
        // catalog row falls back to a sentinel — defensive against a
        // catalog row being removed while an assignment still
        // references it (shouldn't happen but the shape stays valid).
        var definitions = await catalog.ListAllAsync(cancellationToken);
        var keyById = QuotasControllerHelpers.BuildDefinitionKeyMap(definitions);
        var definitionKey = keyById.GetValueOrDefault(existing.DefinitionId, "(deleted)");

        await assignments.DeleteAsync(assignmentId, cancellationToken);

        await auditEmitter.EmitAsync(
            QuotaAuditEvent.AssignmentRemoved,
            new QuotaAuditContext(
                OrgId: currentUser.TenantId,
                ActorUserId: currentUser.UserId,
                DefinitionKey: definitionKey,
                ScopeKind: existing.ScopeKind.ToString(),
                ScopeId: existing.ScopeId,
                AssignmentId: assignmentId),
            cancellationToken);

        return NoContent();
    }
}
