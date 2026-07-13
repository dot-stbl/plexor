using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Parser;

/// <summary>
///     Tests for the polymorphic <see cref="FilterValue" /> hierarchy and
///     <see cref="FilterNode" /> base. The polymorphic shape replaced
///     <c>object?</c> on <c>ComparisonNode.Value</c> — these tests pin the
///     contract (subtype, equality, NullValue singleton).
/// </summary>
public sealed class FilterNodeTests
{
    [Fact(DisplayName = "NullValue is a singleton — Instance returns same reference")]
    public void NullValue_Singleton()
    {
        NullValue.Instance.ShouldBeSameAs(NullValue.Instance);
    }

    [Fact(DisplayName = "FilterNode is abstract — only concrete subtypes can be created")]
    public void FilterNode_AbstractType()
    {
        // Type check via reflection: type is abstract.
        typeof(FilterNode).IsAbstract.ShouldBeTrue();
    }

    [Fact(DisplayName = "ScalarValue equality is value-based")]
    public void ScalarValue_Equality()
    {
        var a = new ScalarValue("hello");
        var b = new ScalarValue("hello");
        var c = new ScalarValue("world");

        a.ShouldBe(b);
        a.ShouldNotBe(c);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact(DisplayName = "FunctionValue equality includes both Name and Argument")]
    public void FunctionValue_Equality()
    {
        var a = new FunctionValue("now", "-7d");
        var b = new FunctionValue("now", "-7d");
        var c = new FunctionValue("now", "-1d");

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    [Fact(DisplayName = "ListValue equality compares ImmutableArray contents")]
    public void ListValue_Equality()
    {
        var a = new ListValue(["a", "b"]);
        var b = new ListValue(["a", "b"]);
        var c = new ListValue(["a", "b", "c"]);

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    [Fact(DisplayName = "NullValue equals itself (record equality)")]
    public void NullValue_Equality()
    {
        var a = NullValue.Instance;
        var b = NullValue.Instance;

        a.ShouldBe(b);
    }

    [Fact(DisplayName = "Different FilterValue subtypes are not equal")]
    public void DifferentSubtypes_NotEqual()
    {
        var scalar = new ScalarValue("hello");
        var fn = new FunctionValue("hello", "");

        scalar.Equals(fn).ShouldBeFalse();
    }

    [Fact(DisplayName = "AndNode/OrNode equality compares children array")]
    public void LogicalNode_Equality()
    {
        var and1 = new AndNode(new ComparisonNode("a", FilterOperator.Eq, new ScalarValue("1")),
                                new ComparisonNode("b", FilterOperator.Eq, new ScalarValue("2")));
        var and2 = new AndNode(new ComparisonNode("a", FilterOperator.Eq, new ScalarValue("1")),
                                new ComparisonNode("b", FilterOperator.Eq, new ScalarValue("2")));
        var and3 = new AndNode(new ComparisonNode("a", FilterOperator.Eq, new ScalarValue("1")));

        and1.ShouldBe(and2);
        and1.ShouldNotBe(and3);
    }

    [Fact(DisplayName = "Empty AndNode is valid (no children)")]
    public void AndNode_Empty()
    {
        var and = new AndNode();
        and.Children.Length.ShouldBe(0);
    }

    [Fact(DisplayName = "FilterValueKind enum values (smoke test for accidental removal)")]
    public void FilterValueKind_AllValuesPresent()
    {
        Enum.GetValues<FilterValueKind>().ShouldBe(
        [
            FilterValueKind.Scalar,
            FilterValueKind.QuotedString,
            FilterValueKind.FunctionCall,
        ]);
    }
}
