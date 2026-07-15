// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SpecificationFactory — non-generic entry points for constructing
// specs. CA1000 disallows `static T Method<T>` on generic types because
// it generates one method per closed-generic-per-call-site (JIT bloat);
// pulling the factory out into a non-generic type avoids the warning
// AND lets callers cache a single factory instance via DI if they
// want.
// ============================================================================

using System.Linq.Expressions;

namespace Plexor.Shared.Persistence;

/// <summary>
///     Static factory methods for <see cref="Specification{T, TResult}" />.
/// </summary>
public static class SpecificationFactory
{
    /// <summary>
    ///     Identity spec — no projection, returns entity rows directly.
    /// </summary>
    public static Specification<T, T> Identity<T>() where T : class
    {
        return new Specification<T, T>(projection: null);
    }

    /// <summary>
    ///     Projection spec — caller passes the Select expression that
    ///     shapes the result rows.
    /// </summary>
    public static Specification<T, TResult> Default<T, TResult>(
        Expression<Func<T, TResult>> projection)
        where T : class
    {
        return new Specification<T, TResult>(projection);
    }
}
