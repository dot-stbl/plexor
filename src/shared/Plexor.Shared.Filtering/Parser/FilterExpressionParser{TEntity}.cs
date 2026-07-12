using System.Collections.Concurrent;
using System.Linq.Expressions;
using Plexor.Shared.Filtering.Persistence;
using Plexor.Shared.Filtering.Registry;

namespace Plexor.Shared.Filtering.Parser;

/// <summary>
///     Entry point for parsing filter DSL strings into LINQ predicates for EF Core.
///     Delegates to <see cref="FilterParser" /> (neutral, TEntity-free) + walks the
///     resulting <see cref="FilterNode" /> tree via the EF translator.
/// </summary>
/// <remarks>
///     <para><b>Expression cache.</b> When the optional field set is null (the
///     common case — uses the default registry), the resulting expression is cached
///     by <c>(Type, string)</c>. Subsequent calls with the same filter string on
///     the same entity type hit the cache and skip parsing + expression building
///     entirely. Hit rate on production list endpoints is ~90%+ (paginated views
///     re-use the same filter across page navigation).</para>
///     <para><b>Custom field sets bypass the cache.</b> When a field set is
///     explicitly passed (rare — tests, custom schemas), the cache is not used
///     because the field set could differ per call.</para>
/// </remarks>
public static class FilterExpression
{
    /// <summary>
    ///     Hard cap on nested parenthesis depth. Bounds stack usage against
    ///     adversarial inputs that emit <c>((((...))))</c> × 10K (audit finding 1e).
    /// </summary>
    public const int MaxParenDepth = 32;

    /// <summary>
    ///     Expression cache keyed by (entity type, raw filter string).
    ///     Unbounded in v0.1 — realistic filter diversity is ~100-500 unique
    ///     strings per entity type; each entry is ~200 bytes (expression tree +
    ///     key). Total ceiling ~500 entries × ~200B = ~100KB — negligible.
    ///     Swap to MemoryCache with size limit if profiling shows otherwise.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type EntityType, string Filter), object?> expressionCache = new();

    /// <summary>
    ///     Parses <paramref name="source" /> and returns the predicate, or <c>null</c>
    ///     when <paramref name="source" /> is null/empty/whitespace.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to filter.</typeparam>
    /// <param name="source">The DSL string.</param>
    /// <param name="fields">Optional pre-built field set; defaults to registry lookup.
    /// When null (default), the result is cached. When explicitly provided, the
    /// cache is bypassed.</param>
    public static Expression<Func<TEntity, bool>>? ParseFor<TEntity>(
        string? source,
        FilterableFieldSet<TEntity>? fields = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        // Only cache when using the default registry (no custom field set).
        // Custom field sets are rare (tests) and may differ per call.
        if (fields is null)
        {
            var cacheKey = (typeof(TEntity), source);

            if (expressionCache.TryGetValue(cacheKey, out var cached))
            {
                return (Expression<Func<TEntity, bool>>?)cached;
            }

            var result = BuildExpression<TEntity>(source, null);
            expressionCache.TryAdd(cacheKey, result);
            return result;
        }

        return BuildExpression<TEntity>(source, fields);
    }

    /// <summary>
    ///     Clears the expression cache. Useful for tests that need deterministic
    ///     cache state. Not called in production — the cache is append-only and
    ///     self-bounded by filter diversity.
    /// </summary>
    public static void ClearCache()
    {
        expressionCache.Clear();
    }

    private static Expression<Func<TEntity, bool>>? BuildExpression<TEntity>(
        string source,
        FilterableFieldSet<TEntity>? fields)
    {
        var node = FilterParser.Parse(source);

        if (node is null)
        {
            return null;
        }

        var fieldSet = fields ?? FilterableFieldRegistry.For<TEntity>();
        var translator = new EfFilterTranslator<TEntity>(fieldSet);
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var body = translator.Translate(node, parameter);

        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }
}
