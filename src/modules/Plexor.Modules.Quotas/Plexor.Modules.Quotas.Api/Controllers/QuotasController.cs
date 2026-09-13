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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plexor.Modules.Quotas.Api.Models;
using Plexor.Modules.Quotas.Application.Quotas;
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
}
