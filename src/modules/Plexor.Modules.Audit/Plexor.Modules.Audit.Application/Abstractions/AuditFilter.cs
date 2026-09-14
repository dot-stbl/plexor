using Plexor.Modules.Audit.Application.Common;

namespace Plexor.Modules.Audit.Application.Abstractions;

/// <summary>
///     Filter envelope passed to <see cref="IAuditStore.QueryAsync" />.
///     Every member is optional — a fresh <c>AuditFilter()</c> with
///     no set fields matches every audit entry in the store (used by
///     admin / SIEM queries).
/// </summary>
/// <remarks>
///     <para><b>AND-only.</b> Every set member is AND-ed into the
///     WHERE clause. There's no OR; cross-axis filters compose at the
///     caller level (callers issue two queries and union in memory).
///     Rationale: OR semantics on audit filters are a footgun — they
///     make "what happened between X and Y" answers non-deterministic.</para>
///     <para><b>Pagination.</b> <see cref="Page" /> is 1-based;
///     <see cref="PageSize" /> defaults to 50. The implementation
///     clamps <see cref="PageSize" /> to a sensible upper bound
///     (e.g. 500) — the caller should not rely on huge pages.</para>
/// </remarks>
/// <param name="OrgId">Filter by tenant. <c>null</c> = every tenant (admin-only path).</param>
/// <param name="ActorId">Filter by actor id. <c>null</c> = any actor.</param>
/// <param name="Action">Filter by exact action string (e.g. <c>"cluster.create"</c>). <c>null</c> = any action.</param>
/// <param name="ResourceType">Filter by resource type label (e.g. <c>"cluster"</c>). <c>null</c> = any.</param>
/// <param name="ResourceId">Filter by resource id. <c>null</c> = any.</param>
/// <param name="TimeRange">Filter by occurrence window. <c>null</c> = any time.</param>
/// <param name="Page">1-based page number. <c>1</c> by default.</param>
/// <param name="PageSize">Items per page. <c>50</c> by default.</param>
public sealed record AuditFilter(
    Guid? OrgId = null,
    Guid? ActorId = null,
    string? Action = null,
    string? ResourceType = null,
    Guid? ResourceId = null,
    TimeRange? TimeRange = null,
    int Page = 1,
    int PageSize = 50);
