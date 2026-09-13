using Plexor.Modules.Audit.Application.Common;

namespace Plexor.Modules.Audit.Application.Abstractions;

/// <summary>
///     Append-only audit log port. The Plexor audit pipeline writes
///     through this contract; the underlying storage is EF Core against
///     <c>atlas.audit_entries</c> in production and an in-memory
///     implementation in tests.
/// </summary>
/// <remarks>
///     <para><b>Append-only by contract.</b> This interface exposes
///     only <c>Record*</c> and <c>Query*</c> methods — no
///     <c>Update</c> or <c>Delete</c>. The contract is the first
///     line of defence; the EF implementation additionally issues a
///     <c>REVOKE UPDATE, DELETE ON atlas.audit_entries FROM PUBLIC</c>
///     statement in the <c>AddAuditSchema</c> migration so the
///     database refuses to honour the methods even if they were
///     added.</para>
///     <para><b>Why a port, not a static logger.</b> Auditing is a
///     cross-cutting concern (every command handler records on
///     success / failure). Centralising through DI keeps the call
///     site short and lets tests substitute an in-memory store
///     without spinning up Postgres.</para>
///     <para><b>Time semantics.</b> <see cref="AuditEntry.OccurredAt" />
///     is set by the caller, not by the store. The store does not
///     stamp <c>now()</c> — clock skew between application nodes
///     would otherwise produce out-of-order rows for the same logical
///     action.</para>
/// </remarks>
public interface IAuditStore
{
    /// <summary>
    ///     Append a single audit entry. Returns when the row is
    ///     persisted (committed, when the caller's transaction
    ///     commits — implementations are scoped to a <c>DbContext</c>).
    /// </summary>
    /// <param name="entry">The entry to persist. Id is supplied by the caller.</param>
    /// <param name="cancellationToken">Forwarded to every IO call.</param>
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Append a batch of audit entries in a single round-trip.
    ///     Implementations may batch the underlying INSERT (EF Core's
    ///     <c>AddRange</c>) but commit semantics are unchanged — the
    ///     batch shares the caller's transaction scope.
    /// </summary>
    /// <param name="entries">Entries to persist. Empty list is a no-op.</param>
    /// <param name="cancellationToken">Forwarded to every IO call.</param>
    public Task RecordBatchAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Read audit entries matching <paramref name="filter" />,
    ///     ordered by <c>occurred_at DESC</c>, then <c>id</c> as a
    ///     deterministic tie-breaker. Pagination is applied via
    ///     <see cref="AuditFilter.Page" /> + <see cref="AuditFilter.PageSize" />.
    /// </summary>
    /// <param name="filter">Filter envelope (every member optional).</param>
    /// <param name="cancellationToken">Forwarded to every IO call.</param>
    /// <returns>
    ///     Matching entries, paginated. May be empty. The total
    ///     count is not returned — callers that need it can re-run
    ///     with a wider page size or implement a count query out of
    ///     band.
    /// </returns>
    public Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Read every audit entry produced by the actor with id
    /// <paramref name="actorId" /> within the supplied
    /// <paramref name="range" />, ordered by <c>occurred_at DESC</c>.
    /// </summary>
    /// <remarks>
    ///     Cross-tenant by design — administrative tools use this to
    ///     reconstruct "everything actor X did" without joining on
    ///     <c>OrgId</c>. Non-admin callers must additionally filter
    ///     the result list by their tenant in memory.
    /// </remarks>
    /// <param name="actorId">Actor id (sigil.users.id / forge.nodes.id / etc.).</param>
    /// <param name="range">Time window (inclusive start, exclusive end).</param>
    /// <param name="cancellationToken">Forwarded to every IO call.</param>
    public Task<IReadOnlyList<AuditEntry>> QueryByActorAsync(
        Guid actorId,
        TimeRange range,
        CancellationToken cancellationToken = default);
}
