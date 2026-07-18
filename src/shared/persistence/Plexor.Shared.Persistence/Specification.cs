// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Specification<T, TResult> — fluent base class for composing
// query criteria. Every With* helper returns a NEW immutable spec
// (decorator pattern) wrapping the previous one — specs are
// append-only value objects, never mutated. Subclasses override the
// ctor to declare the baseline filter/order/projection; callers
// compose further with With* chains on top.
// ============================================================================

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Plexor.Shared.Persistence;

/// <summary>
///     Fluent base class for <see cref="ISpecification{T, TResult}" />.
/// Construct via <see cref="SpecificationFactory.Default{T, TResult}(Expression{Func{T, TResult}})" /> for
/// projection-bearing specs or <see cref="SpecificationFactory.Identity{T}" /> for
/// entity-returning specs; subclass for reusable filter+projection
/// compositions.
/// </summary>
/// <typeparam name="T">Entity table row.</typeparam>
/// <typeparam name="TResult">Projection returned to the caller.</typeparam>
/// <param name="projection"></param>
/// <remarks>
///     Construct from an optional projection expression. Most
///     callers use the <see cref="SpecificationFactory.Default{T, TResult}(Expression{Func{T, TResult}})" /> /
///     <see cref="SpecificationFactory.Identity{T}" /> factory methods; subclass ctors
///     pass the projection explicitly.
/// </remarks>
public class Specification<T, TResult>(Expression<Func<T, TResult>>? projection) : ISpecification<T, TResult>
    where T : class
{
    private readonly List<Expression<Func<T, bool>>> whereClauses = [];
    private Func<IQueryable<T>, IOrderedQueryable<T>>? orderByClause;
    private bool asNoTracking;
    private int? skip;
    private int? take;

    /// <summary>
    ///     The Select(...) projection. Subclasses can set this in their
    ///     constructor or via fluent With* helpers; read-only after
    ///     construction (mutations through the field bypass the
    ///     immutable contract).
    /// </summary>
    protected Expression<Func<T, TResult>>? Projection { get; init; } = projection;

    /// <inheritdoc />
    IQueryable<T> ISpecification<T>.Apply(IQueryable<T> query)
    {
        return ComposeEntityQuery(query);
    }

    /// <inheritdoc />
    public IQueryable<TResult> Apply(IQueryable<T> query)
    {
        var composed = ComposeEntityQuery(query);
        return Projection is null ? composed.Cast<TResult>() : composed.Select(Projection);
    }

    /// <summary>
    ///     Apply filter/order/tracking to the entity queryable. Shared
    ///     between the identity (<see cref="ISpecification{T}" />) and
    ///     the projected (<see cref="ISpecification{T, TResult}" />)
    ///     surfaces — same SQL pipeline, projection is a separate
    ///     step applied after composition.
    /// </summary>
    /// <param name="query"></param>
    private IQueryable<T> ComposeEntityQuery(IQueryable<T> query)
    {
        var q = query;
        if (asNoTracking)
        {
            q = q.AsNoTracking();
        }

        foreach (var clause in whereClauses)
        {
            q = q.Where(clause);
        }

        if (orderByClause is not null)
        {
            q = orderByClause(q);
        }

        if (skip is { } s && take is { } t)
        {
            q = q.Skip(s).Take(t);
        }

        return q;
    }

    /// <summary>
    ///     Add a Where clause. Returns a new immutable spec wrapping
    ///     this one — fluent chains never mutate state.
    /// </summary>
    /// <param name="predicate"></param>
    public Specification<T, TResult> WithWhere(Expression<Func<T, bool>> predicate)
    {
        var copy = (Specification<T, TResult>)MemberwiseClone();
        copy.whereClauses.Add(predicate);
        return copy;
    }

    /// <summary>
    ///     Set OrderBy. Replaces any prior OrderBy.
    /// </summary>
    /// <typeparam name="TKey">Type of the ordering key.</typeparam>
    /// <param name="keySelector">Key selector expression.</param>
    public Specification<T, TResult> WithOrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        var copy = (Specification<T, TResult>)MemberwiseClone();
        copy.orderByClause = q => q.OrderBy(keySelector);
        return copy;
    }

    /// <summary>
    ///     Set OrderByDescending. Replaces any prior OrderBy.
    /// </summary>
    /// <typeparam name="TKey">Type of the ordering key.</typeparam>
    /// <param name="keySelector">Key selector expression.</param>
    public Specification<T, TResult> WithOrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        var copy = (Specification<T, TResult>)MemberwiseClone();
        copy.orderByClause = q => q.OrderByDescending(keySelector);
        return copy;
    }

    /// <summary>Mark the query as no-tracking (read-only).</summary>
    public Specification<T, TResult> AsNoTracking()
    {
        var copy = (Specification<T, TResult>)MemberwiseClone();
        copy.asNoTracking = true;
        return copy;
    }

    /// <summary>
    ///     Apply Skip + Take on the final queryable. In-spec paging is
    ///     useful for tests + small list endpoints; for URL-driven
    ///     paging prefer <c>Repository&lt;T&gt;.PageAsync(spec, page, pageSize, ...)</c>.
    /// </summary>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    public Specification<T, TResult> Paginate(int page, int pageSize)
    {
        var copy = (Specification<T, TResult>)MemberwiseClone();
        copy.skip = (page - 1) * pageSize;
        copy.take = pageSize;
        return copy;
    }
}
