using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Audit.Application.Abstractions;
using Plexor.Modules.Audit.Application.Common;
using Plexor.Modules.Audit.Infrastructure.Persistence.Mappers;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

/// <summary>
///     EF Core adapter for <see cref="IAuditStore" />. Append-only by
///     contract (no <c>Update</c> / <c>Delete</c> methods on the
///     port) and by database (the <c>AddAuditSchema</c> migration
///     REVOKEs UPDATE / DELETE on <c>atlas.audit_entries</c> from
///     PUBLIC).
/// </summary>
/// <remarks>
///     <para><b>Append-only by construction.</b> This class exposes
///     no <c>SaveChangesAsync</c> call to the caller; every write
///     path goes through <c>AddAsync</c> / <c>AddRangeAsync</c>
///     followed by an internal <c>SaveChangesAsync</c>. The
///     implementation never calls <c>Update</c> or <c>Remove</c> —
///     the port contract forbids them, and the database refuses
///     them anyway.</para>
///     <para><b>Query translation.</b> <see cref="QueryAsync" />
///     applies <see cref="AuditFilter" /> predicates directly on the
///     EF queryable. Pagination is the standard skip+take; ordering
///     is <c>occurred_at DESC, id</c> for a stable timeline.</para>
///     <para><b>AsNoTracking.</b> All reads use
///     <c>AsNoTracking()</c> — audit entries are never updated, so
///     the change tracker would just be overhead.</para>
///     <para><b>Lifetime.</b> Scoped — holds the scoped
///     <see cref="AuditDbContext" />.</para>
/// </remarks>
/// <param name="db">The module-scoped EF context.</param>
public sealed class EfAuditStore(AuditDbContext db) : IAuditStore
{
    private const int MaxPageSize = 500;

    /// <inheritdoc />
    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await db.AuditEntries.AddAsync(AuditEntryMapper.ToRecord(entry), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordBatchAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        await db.AuditEntries.AddRangeAsync(
            entries.Select(AuditEntryMapper.ToRecord),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = db.AuditEntries.AsNoTracking();

        if (filter.OrgId is { } orgId)
        {
            query = query.Where(entry => entry.OrgId == orgId);
        }

        if (filter.ActorId is { } actorId)
        {
            query = query.Where(entry => entry.ActorId == actorId);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(entry => entry.Action == action);
        }

        if (filter.ResourceType is { } resourceType)
        {
            query = query.Where(entry => entry.ResourceType == resourceType);
        }

        if (filter.ResourceId is { } resourceId)
        {
            query = query.Where(entry => entry.ResourceId == resourceId);
        }

        if (filter.TimeRange is { } range)
        {
            query = query.Where(entry => entry.OccurredAt >= range.Start)
                .Where(entry => entry.OccurredAt < range.End);
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        var records = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return records.Select(AuditEntryMapper.ToDomain).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEntry>> QueryByActorAsync(
        Guid actorId,
        TimeRange range,
        CancellationToken cancellationToken = default)
    {
        var records = await db.AuditEntries
            .AsNoTracking()
            .Where(entry => entry.ActorId == actorId)
            .Where(entry => entry.OccurredAt >= range.Start)
            .Where(entry => entry.OccurredAt < range.End)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return records.Select(AuditEntryMapper.ToDomain).ToArray();
    }
}
