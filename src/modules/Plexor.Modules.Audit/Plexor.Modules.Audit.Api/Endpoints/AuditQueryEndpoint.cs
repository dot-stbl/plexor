// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditQueryEndpoint — GET /api/v1/audit?action=&org_id=&actor_user_id=
//                          &since=&before=&limit=
// Tenant-scoped admin read surface for the append-only audit log
// (Phase 5.2). The endpoint always filters by currentUser.TenantId
// — a caller in Org X cannot see Org Y's rows, regardless of the
// optional org_id query parameter (which v1 ignores; reserved for a
// future super-admin cross-org view).
//
// Pagination is offset-based (capped at 500 rows; default 100).
// Cursor pagination lands in 5.3 alongside the admin UI.
// ============================================================================

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Audit.Api.Models;
using Plexor.Modules.Audit.Domain.Entities;
using Plexor.Modules.Audit.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Shared.Authorization;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Kernel.Audit;

namespace Plexor.Modules.Audit.Api.Endpoints;

/// <summary>
///     Minimal-API endpoint that surfaces the tenant-scoped audit
///     timeline to admin UI hooks. Read-only; the emit path
///     (<c>DbAuditEmitter</c>) writes the rows, this endpoint reads
///     them back with bounded filters + pagination.
/// </summary>
/// <remarks>
///     <para><b>Tenant scoping.</b> The endpoint always forces
///     <c>org_id = currentUser.TenantId</c>. The optional
///     <c>org_id</c> query parameter is intentionally ignored in
///     v1 — a caller in Org X cannot enumerate Org Y's rows. A
///     future super-admin role with a cross-org grant would
///     re-enable the filter; Phase 5+.</para>
///     <para><b>Ordering.</b> <c>ORDER BY occurred_at DESC</c> +
///     bounded <c>LIMIT</c> — every admin UI page renders the
///     most recent events first. The
///     <c>(org_id, occurred_at DESC)</c> index added in 5.1 backs
///     this scan; the
///     <c>(action, occurred_at DESC)</c> index backs the
///     single-action admin filter.</para>
///     <para><b>Payload parsing.</b> <c>payload_json</c> is parsed
///     on the read path (cheap — single small dict per row,
///     bounded by the 500-row cap). A malformed payload surfaces
///     as a single <c>_raw</c> entry so a single bad row never
///     fails the whole response.</para>
/// </remarks>
public static class AuditQueryEndpoint
{
    /// <summary>Stable route name referenced by the OpenAPI
    /// document generator. File-scoped per
    /// <c>constructors-and-fields.md</c>.</summary>
    private const string AuditListRouteName = "audit-list";

    /// <summary>Default page size when <c>limit</c> is omitted.</summary>
    private const int DefaultLimit = 100;

    /// <summary>Hard upper bound on <c>limit</c>; caps memory use
    /// + the worst-case Postgres round-trip size.</summary>
    private const int MaxLimit = 500;

    /// <summary>
    ///     Map <c>GET /api/v1/audit</c> against
    ///     <paramref name="app" />. Returns the same builder for
    ///     chaining.
    /// </summary>
    /// <param name="app">The host's endpoint route builder.</param>
    /// <returns>The same <paramref name="app" />, for chaining.</returns>
    public static IEndpointRouteBuilder MapAuditQuery(this IEndpointRouteBuilder app)
    {

        // The endpoint URL composes from ApiRoutes.Base so the
        // /api/v1 prefix lives in one place. Minimal APIs can't use
        // the [RequirePermission] attribute directly — the attribute
        // is an MVC IAuthorizeData contract — so we resolve the
        // permission policy name through AuthorizationPolicyNames.For
        // (the same encoder RequirePermissionAttribute uses) and
        // hand it to RequireAuthorization(). The policy provider
        // resolves "permission:audit.read" into one
        // PermissionRequirement on audit.read.
        var path = ApiRoutes.Base + "/audit";
        app.MapGet(path, HandleAsync)
            .WithName(AuditListRouteName)
            .WithTags("audit")
            .RequireAuthorization(AuthorizationPolicyNames.For(AuditPermissions.Read));

        return app;
    }

    /// <summary>
    ///     Endpoint handler. Tenant-scoped query over the audit
    ///     log, ordered by recency, capped at the request's
    ///     <paramref name="limit" /> (clamped to
    ///     [<see cref="DefaultLimit" />,
    ///     <see cref="MaxLimit" />]).
    /// </summary>
    /// <remarks>
    ///     <para><b>Why a static method (not a lambda).</b> Static
    ///     handlers are easier to unit-test than closures and
    ///     match the rest of the Plexor endpoint surface. The
    ///     helper methods that this delegates to are file-static
    ///     per the no-private-methods convention
    ///     (class-layout-and-tooling.md §1a).</para>
    ///     <para><b>Why no <c>org_id</c> query parameter.</b> The
    ///     wire-format URL contract includes <c>?org_id=</c> (for
    ///     forward-compatibility with a Phase 5+ super-admin
    ///     cross-org view), but the endpoint never binds it —
    ///     ASP.NET Core minimal APIs silently ignore unbound query
    ///     parameters, so the URL contract stays stable without
    ///     the parameter passing through the handler. Documented
    ///     in the class remarks; a future super-admin role
    ///     re-enables the binding via a dedicated
    ///     <c>?cross_org=true</c> flag rather than reusing
    ///     <c>org_id</c> (avoids an ambiguous "caller-supplied
    ///     org" semantic).</para>
    /// </remarks>
    internal static async Task<IResult> HandleAsync(
        [FromQuery] string? action,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? since,
        [FromQuery] DateTimeOffset? before,
        [FromQuery] int? limit,
        AuditDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var effectiveOrgId = currentUser.TenantId;

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var rows = await AuditQueryFilters.ApplyAsync(
            db.AuditEntries,
            effectiveOrgId,
            action,
            actorUserId,
            since,
            before,
            effectiveLimit,
            cancellationToken);

        var response = AuditQueryProjection.MapToResponse(rows);
        return Results.Ok(response);
    }

    /// <summary>
    ///     Defensive payload parser. Never throws — a malformed
    ///     <c>payload_json</c> column on a single row is
    ///     surfaced as a single <c>_raw</c> entry, not a 500 on
    ///     the whole response. Uses
    ///     <see cref="JsonSerializerOptions.Web" /> per
    ///     anti-patterns §6 (no inline <c>new
    ///     JsonSerializerOptions(...)</c>).
    /// </summary>
    /// <param name="json">The stored <c>payload_json</c> string.</param>
    /// <returns>Parsed dict, or a single <c>_raw</c>-keyed dict
    /// when the value is not a JSON object.</returns>
    internal static IReadOnlyDictionary<string, object?> ParsePayload(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                json,
                JsonSerializerOptions.Web);
            return parsed ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?> { ["_raw"] = json };
        }
    }
}

/// <summary>
///     LINQ composition for the audit query — extracted so the
///     handler stays a thin orchestrator (class-layout-and-tooling.md
///     §1a). The filter set is small enough that one helper is
///     clearer than a chain of <c>QuerySpec</c> types.
/// </summary>
internal static class AuditQueryFilters
{
    /// <summary>
    ///     Apply the tenant scope + the optional
    ///     <paramref name="action" />, <paramref name="actorUserId" />,
    ///     <paramref name="since" />, <paramref name="before" />
    ///     filters and return the most recent
    ///     <paramref name="effectiveLimit" /> rows.
    /// </summary>
    internal static async Task<List<AuditEntry>> ApplyAsync(
        IQueryable<AuditEntry> source,
        Guid effectiveOrgId,
        string? action,
        Guid? actorUserId,
        DateTimeOffset? since,
        DateTimeOffset? before,
        int effectiveLimit,
        CancellationToken cancellationToken)
    {

        // The OrgId filter can't use a `static` lambda because it
        // captures `effectiveOrgId`; everything downstream is closure-
        // free and stays `static` for the allocation win (EF Core
        // 10 compiles non-static lambdas to per-call closures; static
        // ones become cached delegates).
        var query = source
            .AsNoTracking()
            .Where(entry => entry.OrgId == effectiveOrgId);

        if (action is not null)
        {
            query = query.Where(entry => entry.Action == action);
        }

        if (actorUserId is { } actor)
        {
            query = query.Where(entry => entry.ActorUserId == actor);
        }

        if (since is { } sinceBoundary)
        {
            query = query.Where(entry => entry.OccurredAt >= sinceBoundary);
        }

        if (before is { } beforeBoundary)
        {
            query = query.Where(entry => entry.OccurredAt < beforeBoundary);
        }

        return await query
            .OrderByDescending(static entry => entry.OccurredAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
///     Entity → wire projection. Lives next to the endpoint so the
///     shape stays co-located; the handler is the only caller.
/// </summary>
internal static class AuditQueryProjection
{
    /// <summary>
    ///     Project each <see cref="AuditEntry" /> row to its
    ///     <see cref="AuditQueryResponse" /> wire DTO.
    /// </summary>
    internal static List<AuditQueryResponse> MapToResponse(
        IEnumerable<AuditEntry> rows)
    {
        return rows
            .Select(static entry => new AuditQueryResponse
            {
                Id = entry.Id,
                Action = entry.Action,
                OrgId = entry.OrgId,
                ActorUserId = entry.ActorUserId,
                TargetKind = entry.TargetKind,
                TargetId = entry.TargetId,
                Payload = AuditQueryEndpoint.ParsePayload(entry.PayloadJson),
                OccurredAt = entry.OccurredAt,
            })
            .ToList();
    }
}
