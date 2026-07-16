// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ISpecification<T> / ISpecification<T, TResult> — composition root for
// query criteria. Filters, order, projection, paging flags — all the
// boilerplate that turns a DbSet<T>.Where(...) chained dance into a
// single composable object.
//
// Two type parameters always: T = entity row, TResult = projection
// (DTO, or T when projection isn't needed). Composing specs (decorator
// pattern) lets one spec wrap + extend another without subclass
// explosion.
// ============================================================================

namespace Plexor.Shared.Persistence;

/// <summary>
///     Marker for "this query has no projection" — entity columns
///     flow through to the caller 1:1.
/// </summary>
/// <typeparam name="T">Entity table row.</typeparam>
public interface ISpecification<T> where T : class
{
    /// <summary>
    ///     Apply filter/order/tracking flags to the incoming
    ///     queryable. Returns <see cref="IQueryable{T}" /> — the spec
    ///     does not own projection (pass a separate
    ///     <see cref="System.Linq.Expressions.Expression{TDelegate}" />
    ///     to <c>Select</c> if you need a projection).
    /// </summary>
    /// <param name="query">The entity queryable to layer filters on top of.</param>
    /// <returns>The post-filter, post-order queryable (no projection).</returns>
    public IQueryable<T> Apply(IQueryable<T> query);
}

/// <summary>
///     Projection-aware spec — extends the filter-only
///     <see cref="ISpecification{T}" /> and adds a Select. The caller
///     receives <typeparamref name="TResult" /> (a DTO record /
///     summary / detail projection), not the raw entity.
/// </summary>
/// <remarks>
///     Repo overload <c>Repository&lt;T&gt;.PageAsync(...)</c> only
///     accepts the filter-only interface because the Filtering DSL
///     always works on entity column names — projection lives
///     separately on the call site.
/// </remarks>
/// <typeparam name="T">Entity table row.</typeparam>
/// <typeparam name="TResult">Projection returned to the caller.</typeparam>
public interface ISpecification<T, TResult> : ISpecification<T> where T : class
{
    /// <summary>
    ///     Re-applies filter/order already-set on this spec via the
    ///     base <see cref="ISpecification{T}.Apply" />, then projects
    ///     onto <typeparamref name="TResult" />.
    /// </summary>
    /// <param name="query">The entity queryable to layer filters on top of.</param>
    /// <returns>The post-filter, post-order, post-projection queryable.</returns>
    public new IQueryable<TResult> Apply(IQueryable<T> query);
}
