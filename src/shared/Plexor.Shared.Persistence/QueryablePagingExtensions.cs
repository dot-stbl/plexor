using Microsoft.EntityFrameworkCore;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering;
using Plexor.Shared.Filtering.Query;

namespace Plexor.Shared.Persistence;

/// <summary>
///     Extensions on <see cref="IQueryable{T}" /> for the canonical paging
///     pattern: count + skip + take + project to <see cref="PageResult{T}" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why one extension.</b> Every paged list endpoint in every
///         module runs the same three-step query (count, slice, project). A
///         single helper keeps the SQL pattern in lockstep — adding paging
///         behaviour (e.g. "prefer keyset over offset for tables > 1M rows")
///         is a one-line change here, not a search across handlers.
///     </para>
///     <para>
///         <b>Two-roundtrip model.</b> <c>Count</c> + <c>ToList</c> are
///         separate queries because Postgres' <c>COUNT(*) OVER()</c> window
///         function requires reading every row before slicing, which is
///         slower for large datasets than a separate count. Splitting the
///         two lets each query hit the right index.
///     </para>
///     <para>
///         <b>AsNoTracking.</b> Read paths only — the caller opts out
///         of change tracking so we don't materialise entities in the
///         tracker. Combined with a projection (<c>.Select(...)</c>) this
///         means the SQL query selects only the columns the projection
///         references.
///     </para>
/// </remarks>
public static class QueryablePagingExtensions
{
    /// <summary>
    ///     Materialise <paramref name="source" /> as a <see cref="PageResult{T}" />,
    ///     using <paramref name="query" /> for page + page-size and a
    ///     separate <c>COUNT</c> query for the total. The source is
    ///     assumed to already carry any filter / sort / projection; this
    ///     extension only applies the paging slice.
    /// </summary>
    /// <typeparam name="T">Row type (typically a summary DTO).</typeparam>
    /// <param name="source">Filtered + sorted + projected IQueryable.</param>
    /// <param name="query">FilterQuery carrying page + pageSize.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public static async Task<PageResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        FilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var normalized = query.Normalized();

        var total = await source.CountAsync(cancellationToken);
        var items = await source
                .Skip(normalized.Skip())
                .Take(normalized.PageSize)
                .ToListAsync(cancellationToken);

        return new PageResult<T>(
            items,
            total,
            normalized.Page,
            normalized.PageSize);
    }
}
