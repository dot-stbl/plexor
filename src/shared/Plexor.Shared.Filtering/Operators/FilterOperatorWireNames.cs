using Plexor.Shared.Filtering.Schema;

namespace Plexor.Shared.Filtering.Operators;

/// <summary>
///     Helper extensions on <see cref="FilterOperator" /> for emitting wire-format
///     names (lowercase kebab identifiers matching the kubb plugin's
///     <c>FilterOperatorName</c> union).
/// </summary>
/// <remarks>
///     <para>
///         The wire names are stable identifiers consumed by the frontend
///         (<see cref="FilterableSchemaTransformer" /> → <c>x-filterable</c>
///         extension → kubb plugin's <c>filterable.types.ts</c>). Renaming
///         any of these is a breaking change for the generated client —
///         the FE generator will refuse unknown names and fall back to
///         not emitting the operator.
///     </para>
///     <para>
///         The mapping follows the convention established in the kubb
///         plugin (<c>eq</c>, <c>notEq</c>, <c>contains</c>, ...). Add a
///         new <see cref="FilterOperator" /> by adding the descriptor in
///         <see cref="FilterOperatorRegistry" /> and the wire-name entry
///         here in lockstep.
///     </para>
/// </remarks>
public static class FilterOperatorWireNames
{
    /// <summary>
    ///     Returns the wire-format names for the operator bits set in
    ///     <paramref name="operators" />. None → empty list.
    /// </summary>
    /// <param name="operators">Bitmask of allowed operators for a field.</param>
    public static IReadOnlyList<string> NamesFor(this FilterOperator operators)
    {
        if (operators == FilterOperator.None)
        {
            return [];
        }

        var names = new List<string>(capacity: 12);
        foreach (var (op, name) in All)
        {
            if ((operators & op) == op && op != FilterOperator.None)
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    ///     Maps each <see cref="FilterOperator" /> to its wire name.
    ///     <para>
    ///         Stable across releases — the kubb plugin deserializes from
    ///         these identifiers. Reordering, renaming, or removing a
    ///         member is a breaking change for generated clients.
    ///     </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<FilterOperator, string> All =
        new Dictionary<FilterOperator, string>
        {
            [FilterOperator.Eq] = "eq",
            [FilterOperator.NotEq] = "notEq",
            [FilterOperator.Contains] = "contains",
            [FilterOperator.StartsWith] = "startsWith",
            [FilterOperator.EndsWith] = "endsWith",
            [FilterOperator.Gt] = "gt",
            [FilterOperator.Gte] = "gte",
            [FilterOperator.Lt] = "lt",
            [FilterOperator.Lte] = "lte",
            [FilterOperator.In] = "in",
            [FilterOperator.NotIn] = "notIn",
            [FilterOperator.IContains] = "iContains",
            [FilterOperator.IStartsWith] = "iStartsWith",
            [FilterOperator.IEndsWith] = "iEndsWith",
            [FilterOperator.IsNull] = "isNull",
            [FilterOperator.IsNotNull] = "isNotNull",
        };
}
