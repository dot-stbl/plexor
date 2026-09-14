using System.Collections.Concurrent;
using Plexor.Modules.Audit.Application.Abstractions;
using Plexor.Modules.Audit.Application.Common;

namespace Plexor.Modules.Audit.Unit.TestDoubles;

/// <summary>
///     In-memory <see cref="IAuditStore" /> double for unit tests.
///     Stores entries in a thread-safe bag and applies filter / actor
///     queries in memory. Mirrors the EF store's semantics (append-
///     only, <c>occurred_at DESC</c> ordering) so test assertions
///     against this double are meaningful for the production EF
///     adapter.
/// </summary>
/// <remarks>
///     <para><b>Why <see cref="ConcurrentBag{T}" />.</b> Append-only
///     writes don't need ordering. The read paths sort by
///     <see cref="AuditEntry.OccurredAt" /> explicitly so the
///     underlying storage order is irrelevant.</para>
///     <para><b>Tests against this double cover:</b> the port
///     contract (every method called, every return shape). The EF
///     store has its own integration tests (TestDb-based) that cover
///     the SQL translation; this double is for handler-level
///     logic.</para>
/// </remarks>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentBag<AuditEntry> store = [];

    /// <inheritdoc />
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        store.Add(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordBatchAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken = default)
    {
        foreach (var entry in entries)
        {
            store.Add(entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AuditEntry> snapshot = store.ToArray();

        if (filter.OrgId is { } orgId)
        {
            snapshot = snapshot.Where(entry => entry.OrgId == orgId).ToArray();
        }

        if (filter.ActorId is { } actorId)
        {
            snapshot = snapshot.Where(entry => entry.ActorId == actorId).ToArray();
        }

        if (filter.Action is { } action)
        {
            snapshot = snapshot.Where(entry => entry.Action == action).ToArray();
        }

        if (filter.ResourceType is { } resourceType)
        {
            snapshot = snapshot.Where(entry => entry.ResourceType == resourceType).ToArray();
        }

        if (filter.ResourceId is { } resourceId)
        {
            snapshot = snapshot.Where(entry => entry.ResourceId == resourceId).ToArray();
        }

        if (filter.TimeRange is { } range)
        {
            snapshot = snapshot
                .Where(entry => entry.OccurredAt >= range.Start && entry.OccurredAt < range.End)
                .ToArray();
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);
        var skip = (page - 1) * pageSize;

        IReadOnlyList<AuditEntry> paged = snapshot
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(paged);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditEntry>> QueryByActorAsync(
        Guid actorId,
        TimeRange range,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AuditEntry> result = store
            .Where(entry => entry.ActorId == actorId && entry.OccurredAt >= range.Start && entry.OccurredAt < range.End)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .ToArray();

        return Task.FromResult(result);
    }
}
