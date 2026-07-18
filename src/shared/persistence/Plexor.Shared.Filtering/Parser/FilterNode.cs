using Plexor.Shared.Filtering.Operators;

namespace Plexor.Shared.Filtering.Parser;

/// <summary>
///     Abstract syntax tree node for a parsed filter expression. Produced by
///     <see cref="FilterParser" /> from a DSL string. Neutral — no TEntity coupling, no
///     EF-specific logic. Each consumer (EF translator, CH translator) walks the tree and
///     produces its own target type.
/// </summary>
public abstract record FilterNode;

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
public abstract record FilterValue;

/// <summary>A single scalar value (bare literal or quoted string) — raw text from the DSL (quotes stripped).</summary>
/// <param name="Raw"></param>
public sealed record ScalarValue(string Raw) : FilterValue;

/// <summary>A list of values for IN/NotIn operators.
/// Stored as <see cref="System.Collections.Immutable.ImmutableArray{T}" /> so
/// repeated cache hits don't re-allocate the backing array — the lexer builds
/// it once on cache miss and the value is preserved verbatim on cache hit.</summary>
/// <param name="Items"></param>
public sealed record ListValue(
    System.Collections.Immutable.ImmutableArray<string> Items) : FilterValue
{
    /// <summary>
    ///     Element-wise equality on the items — the synthesized record
    ///     default compares ImmutableArray by reference; the
    ///     <c>FilterNodeTests.ListValue_Equality</c> contract expects
    ///     content-based equality.
    /// </summary>
    /// <param name="other"></param>
    public bool Equals(ListValue? other)
    {
        if (other is null)
        {
            return false;
        }

        return Items.AsSpan().SequenceEqual(other.Items.AsSpan());
    }

    /// <summary>Hash from item sequence so equal lists share a bucket.</summary>
    public override int GetHashCode()
    {
        var hash = new System.HashCode();
        foreach (var item in Items.AsSpan())
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }
}

/// <summary>No value — used by IsNull/IsNotNull operators.</summary>
public sealed record NullValue : FilterValue
{
    /// <summary>Singleton instance — NullValue carries no state.</summary>
    public static NullValue Instance { get; } = new();

    private NullValue() { }
}

/// <summary>A function call value (e.g. <c>now(-7d)</c>).
/// First positional is the function identifier from the DSL; second is the raw argument text.</summary>
/// <param name="Name"></param>
/// <param name="Argument"></param>
public sealed record FunctionValue(string Name, string Argument) : FilterValue;

// ==================== Comparison / logical nodes ====================

/// <summary>
///     Single comparison: <c>field op value</c>. The value is a polymorphic
///     <see cref="FilterValue" /> subtype — <see cref="ScalarValue" />,
///     <see cref="ListValue" />, <see cref="FunctionValue" />, or
///     <see cref="NullValue" />. Positional: field name, comparison operator,
///     parsed value (pattern-match on the concrete subtype).
/// </summary>
/// <param name="Field"></param>
/// <param name="Operator"></param>
/// <param name="Value"></param>
public sealed record ComparisonNode(
    string Field,
    FilterOperator Operator,
    FilterValue Value) : FilterNode
{
    /// <summary>
    ///     How the consumer should interpret <see cref="Value" />. Defaults to
    ///     <see cref="FilterValueKind.Scalar" />. Init-only so callers set it
    ///     after construction (<c>n.ValueKind = FilterValueKind.FunctionCall;</c>).
    /// </summary>
    public FilterValueKind ValueKind { get; init; } = FilterValueKind.Scalar;
}

/// <summary>
///     Logical AND over child nodes. <c>a ; b ; c</c>. The
///     <see cref="Children" /> array member participates in the synthesized
///     record Equals / GetHashCode via an override below (the default
///     record-synthesised equality would compare the array by reference, not
///     element-wise, breaking the <c>FilterNodeTests.LogicalNode_Equality</c>
///     contract). Positional is the ordered child-node list — empty AND is
///     valid (no-op).
/// </summary>
/// <param name="Children"></param>
public sealed record AndNode(params FilterNode[] Children) : FilterNode
{
    /// <summary>
    ///     Element-wise equality on the children array. The synthesized
    ///     record default would compare the array by reference; the
    ///     <c>FilterNodeTests.LogicalNode_Equality</c> contract requires
    ///     element-wise.
    /// </summary>
    /// <param name="other"></param>
    public bool Equals(AndNode? other)
    {
        if (other is null)
        {
            return false;
        }

        return Children.AsSpan().SequenceEqual(other.Children);
    }

    /// <summary>Hash from ordered child sequence so equal ANDs share a bucket.</summary>
    public override int GetHashCode()
    {
        var hash = new System.HashCode();
        hash.Add(Children.Length);
        foreach (var child in Children)
        {
            hash.Add(child);
        }
        return hash.ToHashCode();
    }
}

/// <summary>
///     Logical OR over child nodes. <c>a | b | c</c>. Element-wise equality
///     override mirrors <see cref="AndNode" /> so both logical operators use
///     the same comparator semantics. Positional is the ordered child-node
///     list — empty OR is valid (no-op).
/// </summary>
/// <param name="Children"></param>
public sealed record OrNode(params FilterNode[] Children) : FilterNode
{
    /// <summary>Element-wise equality on the children array — see <see cref="AndNode" />.</summary>
    /// <param name="other"></param>
    public bool Equals(OrNode? other)
    {
        if (other is null)
        {
            return false;
        }

        return Children.AsSpan().SequenceEqual(other.Children);
    }

    /// <summary>Hash from ordered child sequence — see <see cref="AndNode" />.</summary>
    public override int GetHashCode()
    {
        var hash = new System.HashCode();
        hash.Add(Children.Length);
        foreach (var child in Children)
        {
            hash.Add(child);
        }
        return hash.ToHashCode();
    }
}
