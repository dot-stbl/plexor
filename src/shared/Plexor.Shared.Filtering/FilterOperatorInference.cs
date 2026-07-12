namespace Plexor.Shared.Filtering;

/// <summary>
///     Infers the set of <see cref="FilterOperator" /> a CLR property type supports.
///     Used by the field registry when no explicit operator override is supplied —
///     every public property becomes filterable with operators derived from its type,
///     unless marked <c>[NotMapped]</c>.
/// </summary>
/// <remarks>
///     This is a thin facade over <see cref="FilterOperatorRegistry.OperatorsFor" />.
///     The per-type rules (which operator applies to which CLR type, and the
///     null-op attachment for reference/Nullable&lt;T&gt;) live in the registry —
///     adding an operator there is the only edit needed.
/// </remarks>
public static class FilterOperatorInference
{
    /// <summary>
    ///     Returns the operators supported by <paramref name="type" />, or
    ///     <see cref="FilterOperator.None" /> when the type is not filterable at all
    ///     (in which case the field is excluded from the registry).
    /// </summary>
    /// <param name="type"></param>
    public static FilterOperator Infer(Type type)
    {
        return FilterOperatorRegistry.OperatorsFor(type);
    }
}
