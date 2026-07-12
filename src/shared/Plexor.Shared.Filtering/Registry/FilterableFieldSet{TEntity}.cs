using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace Plexor.Shared.Filtering;

/// <summary>
///     Reflects an entity type into a map of filterable fields. Every public
///     instance property is filterable unless it is annotated <c>[NotMapped]</c> or
///     its CLR type maps to <see cref="FilterOperator.None" />. Operator sets are
///     inferred by <see cref="FilterOperatorInference" />.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
/// <param name="fields"></param>
/// <remarks>Constructs from a pre-built field dictionary.</remarks>
public sealed class FilterableFieldSet<TEntity>(IReadOnlyDictionary<string, FilterableField<TEntity>> fields)
{
    private readonly Dictionary<string, FilterableField<TEntity>> fieldMap = new(fields, StringComparer.OrdinalIgnoreCase);

    /// <summary>All registered fields (for OpenAPI schema emission + diagnostics).</summary>
    public IReadOnlyCollection<FilterableField<TEntity>> All => fieldMap.Values;

    /// <summary>
    ///     Looks up a field by name (case-insensitive). Returns <c>null</c> when the
    ///     field is unknown or excluded from the registry — the parser surfaces this as
    ///     a <see cref="FilterParseException" />.
    /// </summary>
    /// <param name="name"></param>
    public FilterableField<TEntity>? Find(string name)
    {
        return fieldMap.TryGetValue(name, out var field) ? field : null;
    }
}

/// <summary>
///     Builds <see cref="FilterableFieldSet{TEntity}" /> via reflection, and caches
///     the result per entity type. Reflection happens at most once per type.
/// </summary>
public static class FilterableFieldRegistry
{
    private static readonly ConcurrentDictionary<Type, object> cache = new();

    /// <summary>Returns the cached field set for <typeparamref name="TEntity" />, building it on first use.</summary>
    /// <typeparam name="TEntity"></typeparam>
    public static FilterableFieldSet<TEntity> For<TEntity>()
    {
        return (FilterableFieldSet<TEntity>)cache.GetOrAdd(typeof(TEntity), static _ => FilterableFieldSetBuilder.Build<TEntity>());
    }

    /// <summary>
    ///     Returns a typed-erased snapshot of the filterable fields for <paramref name="type" />.
    ///     Used by OpenAPI schema transformers and other reflection-only consumers
    ///     that cannot name a generic <c>TEntity</c> at compile time.
    /// </summary>
    /// <param name="type"></param>
    public static IReadOnlyList<UntypedFilterableField> BuildUntyped(Type type)
    {
        return (IReadOnlyList<UntypedFilterableField>)cache.GetOrAdd(type, static t => FilterableFieldSetBuilder.BuildUntypedCore(t));
    }
}

/// <summary>
///     Field descriptor without a generic entity parameter — for reflection-based
///     consumers (OpenAPI schema transformer, diagnostics). Carries only the static
///     metadata (<see cref="Name" />, <see cref="ValueType" />, <see cref="Operators" />)
///     without the accessor expression, which needs a generic type.
/// </summary>
/// <param name="Name"></param>
/// <param name="ValueType"></param>
/// <param name="Operators"></param>
public sealed record UntypedFilterableField(string Name, Type ValueType, FilterOperator Operators);

file static class FilterableFieldSetBuilder
{
    /// <summary>
    ///     Field exposure contract: every public instance property of an entity is
    ///     filterable unless the property is annotated <c>[NotMapped]</c>, is an
    ///     indexer, or has a CLR type the DSL does not support (e.g. <c>byte[]</c>,
    ///     <c>object</c>). This is an opt-out contract.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <remarks>
    ///     <para>
    ///         Sensitive entities (those carrying credentials, computed hashes, or
    ///         internal state) MUST mark such properties with <c>[NotMapped]</c> at
    ///         declaration time. The DSL has no other exclusion mechanism — adding
    ///         a new public sensitive property without <c>[NotMapped]</c> exposes it
    ///         to filter queries immediately. Treat the contract as "every public
    ///         property is readable until proven otherwise".
    ///     </para>
    ///     <para>
    ///         The audit finding 1g called this out as a low-severity RISK; the
    ///         trade-off chosen here is to keep the developer experience simple
    ///         (no opt-in attributes to remember) over a stricter opt-in contract
    ///         that would require every existing entity to be re-annotated.
    ///     </para>
    /// </remarks>
    public static FilterableFieldSet<TEntity> Build<TEntity>()
    {
        var type = typeof(TEntity);
        var parameter = Expression.Parameter(type, "x");
        var fields = new Dictionary<string, FilterableField<TEntity>>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (PropertyExclusions.IsExcluded(property))
            {
                continue;
            }

            var operators = FilterOperatorInference.Infer(property.PropertyType);

            if (operators == FilterOperator.None)
            {
                continue;
            }

            var accessor = Expression.Lambda<Func<TEntity, object?>>(
                Expression.Convert(Expression.Property(parameter, property), typeof(object)),
                parameter);

            fields[property.Name] = new FilterableField<TEntity>(
                property.Name,
                property.PropertyType,
                operators,
                accessor);
        }

        return new FilterableFieldSet<TEntity>(fields);
    }

    /// <summary>
    ///     Reflection-only variant — produces <see cref="UntypedFilterableField" />
    ///     snapshots without building accessor expressions. Used by the OpenAPI schema
    ///     transformer, which needs only the static metadata (name + operators) per field.
    /// </summary>
    /// <remarks>
    ///     See <see cref="Build{TEntity}" /> for the opt-out contract that applies
    ///     here as well.
    /// </remarks>
    /// <param name="type"></param>
    public static IReadOnlyList<UntypedFilterableField> BuildUntypedCore(Type type)
    {
        var fields = new List<UntypedFilterableField>();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (PropertyExclusions.IsExcluded(property))
            {
                continue;
            }

            var operators = FilterOperatorInference.Infer(property.PropertyType);

            if (operators == FilterOperator.None)
            {
                continue;
            }

            fields.Add(new UntypedFilterableField(property.Name, property.PropertyType, operators));
        }

        return fields;
    }
}

file static class PropertyExclusions
{
    /// <summary>
    ///     Returns <c>true</c> when the property must not be filterable: explicit
    ///     <c>[NotMapped]</c>, or an indexer (cannot be expressed as <c>x =&gt; x.P</c>).
    /// </summary>
    /// <param name="property"></param>
    public static bool IsExcluded(PropertyInfo property)
    {
        return property.GetIndexParameters().Length > 0
               || property.IsDefined(typeof(NotMappedAttribute), true);
    }
}
