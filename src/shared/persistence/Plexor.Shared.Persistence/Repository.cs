// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Repository<T> — abstract base class for all module repositories
// (Ardalis-compatible shape). Read endpoints inject
// `Repository<T>`, write endpoints inject `DbContext` directly per
// architecture/persistence.md (writes stay explicit; aggregates
// span multiple DbSets and need a single SaveChanges).
//
// Subclasses per module are thin: `ClusterRepository(ClusterDbContext db)
// : Repository<Cluster>` — they wire the typed DbSet. Override
// methods only when genuinely custom (e.g. setting eager-loading
// flags the base can't express).
// ============================================================================

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering.Persistence;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Filtering.Registry;

namespace Plexor.Shared.Persistence;

/// <summary>
///     Base class for per-entity read repositories. Subclasses per
///     module:
///     <code>public sealed class ClusterRepository(ClusterDbContext db) : Repository&lt;Cluster&gt;</code>
/// </summary>
/// <typeparam name="T">Entity table row.</typeparam>
public abstract class Repository<T> where T : class
{
    /// <summary>Provided to subclasses — the underlying typed DbSet for this entity.</summary>
    protected abstract IQueryable<T> Query { get; }

    /// <summary>
    ///     Apply spec to the typed DbSet + materialize. Identity
    ///     projection — entity rows flow through to the caller as a
    ///     fully materialized list (indexable, <c>Count</c> = O(1)).
    /// </summary>
    /// <param name="specification">The spec carrying filter/order/tracking.</param>
    /// <param name="cancellationToken">Forwarded to EF Core.</param>
    public virtual async Task<IReadOnlyList<T>> ListAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        // ToArrayAsync is the EF-default materialization for read-only
        // lists — smaller allocation (no List<T> wrapper) and directly
        // implements IReadOnlyList<T> (array structural semantics).
        // Only reach for ToListAsync when the caller mutates the result.
        return await specification.Apply(Query).ToArrayAsync(cancellationToken);
    }

    /// <summary>
    ///     Projection variant — caller passes the Select expression
    ///     separately from the filter spec. Filter/order specs stay on
    ///     <see cref="ISpecification{T}" /> (entity rows); projection is
    ///     applied at the end of the pipeline, after Skip/Take.
    /// </summary>
    public virtual async Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        ISpecification<T> filterSpec,
        Expression<Func<T, TResult>> projection,
        CancellationToken cancellationToken = default)
    {
        return await filterSpec
            .Apply(Query)
            .Select(projection)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    ///     Count with the same predicate the spec carries. Use alongside
    ///     <see cref="ListAsync{TResult}(ISpecification{T}, Expression{Func{T, TResult}}, CancellationToken)" /> for
    ///     manual paged queries; <c>PageAsync</c> combines both atomically.
    /// </summary>
    public virtual async Task<int> CountAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return await specification.Apply(Query).CountAsync(cancellationToken);
    }

    /// <summary>
    ///     Single entity by primary key. EF Core's FindAsync checks the
    ///     change-tracker cache first (cheap) and falls back to DB. For
    ///     composite PKs, key is an anonymous object array; for single
    ///     Guid / int, just the value.
    /// </summary>
    public virtual async Task<T?> GetByIdAsync<TId>(
        TId id,
        CancellationToken cancellationToken = default)
    {
        return await Query.FirstOrDefaultAsync(BuildByIdPredicate(id), cancellationToken);
    }

    /// <summary>
    ///     First match (or null). Spec carries the predicate. For the
    ///     common "fetch by single unique field" pattern, define a
    ///     spec inline via `<c>new Specification&lt;T, T&gt;(projection: null).WithWhere(predicate)</c>`.
    /// </summary>
    public virtual async Task<T?> FirstOrDefaultAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return await specification.Apply(Query).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    ///     Paged read with full URL-driven filtering + sort + paging +
    ///     projection. Composition order:
    ///     <list type="number">
    ///       <item>Spec's filter/order (composable baseline)</item>
    ///       <item><see cref="QueryableFilterExtensions.ApplyFilter{TEntity}(IQueryable{TEntity}, string?, FilterableFieldSet{TEntity}?)" />
    ///             with the URL <see cref="FilterQuery.Filter" /> DSL</item>
    ///       <item><see cref="QueryableFilterExtensions.ApplySort{TEntity}(IQueryable{TEntity}, string?, FilterableFieldSet{TEntity}?)" />
    ///             with the URL <see cref="FilterQuery.Sort" /></item>
    ///       <item>Count over the filtered + sorted set</item>
    ///       <item>Skip + Take for the requested page slice</item>
    ///       <item>Project to <typeparamref name="TResult" /></item>
    ///     </list>
    ///     Spec is an <see cref="ISpecification{T}" /> (entity-row filter/order)
    ///     — projection lives separately so the Filtering DSL can
    ///     always work on entity columns (matching
    ///     <see cref="FilterableFieldSet{T}" /> field names).
    /// </summary>
    /// <param name="filterSpec">Spec carrying filter/order/tracking on
    /// entity rows.</param>
    /// <param name="projection">Select expression that shapes each row
    /// into the caller's <typeparamref name="TResult" />.</param>
    /// <param name="query">URL envelope: Filter, Sort, Page, PageSize.</param>
    /// <param name="fields">Per-entity field registry for DSL validation +
    /// sort field resolution. Built via
    /// <c>FilterableFieldRegistry.For&lt;T&gt;()</c>.</param>
    /// <param name="cancellationToken">Forwarded to EF Core.</param>
    public virtual async Task<PageResult<TResult>> PageAsync<TResult>(
        ISpecification<T> filterSpec,
        Expression<Func<T, TResult>> projection,
        FilterQuery query,
        FilterableFieldSet<T> fields,
        CancellationToken cancellationToken = default)
    {
        var filtered = filterSpec
            .Apply(Query)
            .ApplyFilter<T>(query.Filter, fields)
            .ApplySort<T>(query.Sort, fields);

        var total = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(projection)
            .ToArrayAsync(cancellationToken);
        // T[] from ToArrayAsync is implicitly castable to
        // IReadOnlyList<T> via array's IReadOnlyList<T> implementation
        // — no extra copy needed.
        return new PageResult<TResult>(items, total, query.Page, query.PageSize);
    }

    private static Expression<Func<T, bool>> BuildByIdPredicate<TId>(TId id)
    {
        // Helper: compile `e => e.Id == id` via EF's EF.Property convention.
        // Single-key case (Guid / int / string): we look up the property
        // named "Id" via reflection — name-based, but matches every
        // Plexor entity which uses `Id` as the primary key per
        // architecture/persistence.md.
        var parameter = Expression.Parameter(typeof(T), "e");
        var idProperty = typeof(T).GetProperty("Id")
            ?? throw new InvalidOperationException(
                $"Repository<{typeof(T).Name}>: entity has no 'Id' property — wire a custom FindById or rename the PK.");
        var equality = Expression.Equal(
            Expression.Property(parameter, idProperty),
            Expression.Constant(id, idProperty.PropertyType));
        return Expression.Lambda<Func<T, bool>>(equality, parameter);
    }
}
