using Plexor.Shared.Kernel.Common;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Domain.Entities;

/// <summary>
///     Append-only event log entry for sliding-window rate limiting.
///     One row per authenticated API call; the limiter
///     (<c>IRateLimiter</c>, 4.5.e) computes <c>COUNT(*) WHERE
///     occurred_at &gt; now() - interval '1 hour'</c> against this
///     table per request.
/// </summary>
/// <remarks>
///     <para><b>Why append-mostly.</b> Sliding-window semantics require
///     individual event timestamps to age out individually. A
///     counter-style row would need per-period reset bookkeeping —
///     not worth it when each event is one narrow row.</para>
///     <para><b>Cleanup.</b> A daily <c>BackgroundService</c>
///     (4.5.e) deletes events older than the max period (1h for v1)
///     to keep the table bounded.</para>
///     <para><b>Not filterable.</b> Internal state table; queried
///     through the 4.5.e limiter and the 4.5.g usage endpoints,
///     not the public filter DSL.</para>
/// </remarks>
public sealed class RateLimitEvent : ICreatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>User id (JWT auth) or API key id (service auth).</summary>
    public Guid PrincipalId { get; init; }

    /// <summary>Kind of principal — User / ApiKey. Stored as the
    /// lowercase member name (e.g. <c>"user"</c>) so a future
    /// principal kind doesn't require a migration.</summary>
    public RateLimitPrincipalKind PrincipalKind { get; init; }

    /// <summary>Tenant the principal belongs to (denormalized for
    /// org-aggregate rate-limit checks).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Request path the event was recorded against (bounded
    /// cardinality). Indexed for analytics queries.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>UTC timestamp of the request. Indexed for the
    /// sliding-window SELECT.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
