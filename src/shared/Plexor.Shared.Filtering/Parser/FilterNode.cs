using Plexor.Shared.Filtering.Operators;

namespace Plexor.Shared.Filtering.Parser;

/// <summary>
///     Abstract syntax tree node for a parsed filter expression. Produced by
///     <see cref="FilterParser" /> from a DSL string. Neutral — no TEntity coupling, no
///     EF-specific logic. Each consumer (EF translator, CH translator) walks the tree and
///     produces its own target type.
/// </summary>
public abstract class FilterNode;

/// <summary>
///     How the <see cref="ComparisonNode.Value" /> should be interpreted by the
///     translator. The neutral parser can't tell apart a quoted string <c>"a (b)"</c>
///     from a function call <c>now(-7d)</c> by looking at the string alone — the
///     token kind from the lexer is the only signal.
/// </summary>
public enum FilterValueKind
{
    /// <summary>Bare value (unquoted literal, function argument, or unquoted function call).</summary>
    Scalar = 0,

    /// <summary>Quoted string literal — the value is the text inside the quotes, verbatim.</summary>
    QuotedString = 1,

    /// <summary>
    ///     Function call (e.g. <c>now(-7d)</c>) — the value is the raw text
    ///     <c>"now(-7d)"</c>; the translator evaluates it (e.g. to a DateTimeOffset
    ///     for date/time fields). Detected by the parser from the
    ///     <c>Identifier + OpenParen</c> pattern in the lexer, not from the value
    ///     text alone.
    /// </summary>
    FunctionCall = 2
}

// ==================== Polymorphic filter values ====================

/// <summary>
///     Base for the value carried by a <see cref="ComparisonNode" />. Replaces
///     the old <c>object?</c> — the consumer pattern-matches on the concrete
///     subtype instead of guessing via <c>is string</c> / <c>is List&lt;string&gt;</c>.
/// </summary>
public abstract class FilterValue;

/// <summary>A single scalar value (bare literal or quoted string).</summary>
/// <remarks>Constructs a scalar value.</remarks>
/// <param name="raw">Raw string from the DSL (quotes already stripped).</param>
public sealed class ScalarValue(string raw) : FilterValue
{
    /// <summary>The raw string value from the DSL.</summary>
    public string Raw { get; } = raw;
}

/// <summary>A list of values for IN/NotIn operators.</summary>
/// <remarks>Constructs a list value. <paramref name="items" /> is stored as
/// <see cref="System.Collections.Immutable.ImmutableArray{T}" /> so repeated
/// cache hits don't re-allocate the backing array — the lexer builds it once
/// on cache miss and the value is preserved verbatim on cache hit.</remarks>
/// <param name="items">Raw string items from the DSL (quotes already stripped per item).</param>
public sealed class ListValue(System.Collections.Immutable.ImmutableArray<string> items) : FilterValue
{
    /// <summary>The raw string items from the DSL.</summary>
    public System.Collections.Immutable.ImmutableArray<string> Items { get; } = items;
}

/// <summary>No value — used by IsNull/IsNotNull operators.</summary>
public sealed class NullValue : FilterValue
{
    /// <summary>Singleton instance — NullValue carries no state.</summary>
    public static NullValue Instance { get; } = new();

    private NullValue() { }
}

/// <summary>A function call value (e.g. <c>now(-7d)</c>).</summary>
/// <remarks>Constructs a function-call value.</remarks>
/// <param name="name">Function identifier from the DSL.</param>
/// <param name="argument">Raw argument text from the DSL.</param>
public sealed class FunctionValue(string name, string argument) : FilterValue
{
    /// <summary>The function name (e.g. <c>"now"</c>).</summary>
    public string Name { get; } = name;

    /// <summary>The raw argument text (e.g. <c>"-7d"</c>).</summary>
    public string Argument { get; } = argument;
}

// ==================== Comparison / logical nodes ====================

/// <summary>
///     Single comparison: <c>field op value</c>. The value is a polymorphic
///     <see cref="FilterValue" /> subtype — <see cref="ScalarValue" />,
///     <see cref="ListValue" />, <see cref="FunctionValue" />, or
///     <see cref="NullValue" />.
/// </summary>
/// <param name="Field"></param>
/// <param name="Operator"></param>
/// <param name="Value"></param>
public sealed class ComparisonNode(
    string Field,
    FilterOperator Operator,
    FilterValue Value) : FilterNode
{
    /// <summary>The field name from the DSL.</summary>
    public string Field { get; } = Field;

    /// <summary>The comparison operator.</summary>
    public FilterOperator Operator { get; } = Operator;

    /// <summary>
    ///     The parsed value. Pattern-match on the concrete subtype:
    ///     <see cref="ScalarValue" />, <see cref="ListValue" />,
    ///     <see cref="FunctionValue" />, or <see cref="NullValue" />.
    /// </summary>
    public FilterValue Value { get; } = Value;

    /// <summary>
    ///     How the consumer should interpret <see cref="Value" />. Defaults to
    ///     <see cref="FilterValueKind.Scalar" />.
    /// </summary>
    public FilterValueKind ValueKind { get; init; } = FilterValueKind.Scalar;
}

/// <summary>
///     Logical AND over child nodes. <c>a ; b ; c</c>.
/// </summary>
/// <param name="children"></param>
public sealed class AndNode(params FilterNode[] children) : FilterNode
{
    /// <summary>The child nodes.</summary>
    public FilterNode[] Children { get; } = children;
}

/// <summary>
///     Logical OR over child nodes. <c>a | b | c</c>.
/// </summary>
/// <param name="children"></param>
public sealed class OrNode(params FilterNode[] children) : FilterNode
{
    /// <summary>The child nodes.</summary>
    public FilterNode[] Children { get; } = children;
}
