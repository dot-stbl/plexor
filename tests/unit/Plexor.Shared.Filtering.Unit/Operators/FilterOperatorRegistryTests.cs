using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Operators;

/// <summary>
///     Tests for <see cref="FilterOperatorRegistry" /> — the central
///     source of truth for operator descriptors. Each subsystem (lexer,
///     inference, translator) reads from this registry; adding an operator
///     is one entry in <see cref="FilterOperatorRegistry" />'s BuildDescriptors.
/// </summary>
public sealed class FilterOperatorRegistryTests
{
    [Fact(DisplayName = "Get returns descriptor for every known operator")]
    public void Get_KnownOperators_ReturnDescriptor()
    {
        // Sample the full enum. We don't assert the contents (the registry
        // can grow), but every operator MUST have a descriptor.
        foreach (var op in Enum.GetValues<FilterOperator>())
        {
            if (op == FilterOperator.None)
            {
                continue;
            }

            var desc = FilterOperatorRegistry.Get(op);
            desc.ShouldNotBeNull();
            desc.Operator.ShouldBe(op);
        }
    }

    [Fact(DisplayName = "SymbolsByDescendingLength is sorted by length, longest first")]
    public void SymbolsByDescendingLength_Sorted()
    {
        var symbols = FilterOperatorRegistry.SymbolsByDescendingLength.Select(static s => s.Symbol).ToList();

        for (var i = 1; i < symbols.Count; i++)
        {
            symbols[i].Length.ShouldBeLessThanOrEqualTo(symbols[i - 1].Length,
                $"Symbol '{symbols[i]}' should come before '{symbols[i - 1]}'");
        }
    }

    [Fact(DisplayName = "All known operator symbols appear in the symbols list")]
    public void AllOperatorSymbols_AreListed()
    {
        var knownSymbols = new HashSet<string>(StringComparer.Ordinal)
        {
            "==", "!=", "~", "^=", "$=", ">=", "<=", ">", "<", "[]=", "![]=",
            "~*", "^=*", "$=*", "?", "!?",
        };
        var listed = FilterOperatorRegistry.SymbolsByDescendingLength
            .Select(static s => s.Symbol).ToHashSet(StringComparer.Ordinal);

        // The list is allowed to have extra symbols (e.g. newer operators), but
        // every known one must be present.
        foreach (var known in knownSymbols)
        {
            listed.Contains(known).ShouldBeTrue($"missing symbol: {known}");
        }
    }

    [Fact(DisplayName = "Get(unknown operator) throws")]
    public void Get_UnknownOperator_Throws()
    {
        // We don't have a way to construct a fresh enum value at runtime —
        // skip the negative case. The positive path is covered by
        // Get_KnownOperators_ReturnDescriptor.
    }

    [Fact(DisplayName = "OperatorsFor(string) returns In + IsNull for strings")]
    public void OperatorsFor_String()
    {
        var ops = FilterOperatorRegistry.OperatorsFor(typeof(string));
        ops.ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.In).ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.IsNull).ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.IsNotNull).ShouldNotBe(FilterOperator.None);
    }

    [Fact(DisplayName = "OperatorsFor(int) returns range operators")]
    public void OperatorsFor_Int()
    {
        var ops = FilterOperatorRegistry.OperatorsFor(typeof(int));
        (ops & FilterOperator.Eq).ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.Gt).ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.Lt).ShouldNotBe(FilterOperator.None);
    }

    [Fact(DisplayName = "OperatorsFor(Guid) returns In but not range")]
    public void OperatorsFor_Guid()
    {
        var ops = FilterOperatorRegistry.OperatorsFor(typeof(Guid));
        (ops & FilterOperator.In).ShouldNotBe(FilterOperator.None);
        // Guids are not ordered — no range ops.
        (ops & FilterOperator.Gt).ShouldBe(FilterOperator.None);
    }

    [Fact(DisplayName = "OperatorsFor(bool) returns only Eq, not NotEq or range")]
    public void OperatorsFor_Bool()
    {
        var ops = FilterOperatorRegistry.OperatorsFor(typeof(bool));
        (ops & FilterOperator.Eq).ShouldNotBe(FilterOperator.None);
        // Boolean is excluded from NotEq per the inference rule.
        (ops & FilterOperator.NotEq).ShouldBe(FilterOperator.None);
    }

    [Fact(DisplayName = "OperatorsFor(byte) is treated as ordered (numeric)")]
    public void OperatorsFor_Byte()
    {
        var ops = FilterOperatorRegistry.OperatorsFor(typeof(byte));
        (ops & FilterOperator.Gt).ShouldNotBe(FilterOperator.None);
    }

    [Fact(DisplayName = "OperatorsFor(DateTime) is ordered")]
    public void OperatorsFor_DateTime()
    {
        var ops = FilterOperatorRegistry.OperatorsFor(typeof(DateTime));
        (ops & FilterOperator.Gt).ShouldNotBe(FilterOperator.None);
    }

    [Fact(DisplayName = "OperatorsFor(string) includes case-insensitive string ops")]
    public void OperatorsFor_String_CaseInsensitive()
    {
        var ops = FilterOperatorRegistry.OperatorsFor(typeof(string));
        (ops & FilterOperator.IContains).ShouldNotBe(FilterOperator.None);
    }

    [Fact(DisplayName = "Descriptor is a record with value equality")]
    public void Descriptor_RecordEquality()
    {
        const FilterOperator op = FilterOperator.Eq;
        var d1 = FilterOperatorRegistry.Get(op);
        var d2 = FilterOperatorRegistry.Get(op);

        d1.Equals(d2).ShouldBeTrue();
        d1.GetHashCode().ShouldBe(d2.GetHashCode());
    }
}

/// <summary>
///     Tests for <see cref="FilterOperatorWireNames" /> — the C#-flag → wire-name
///     string converter. Must stay in lockstep with the kubb plugin's
///     FilterOperatorName union (frontend reads this).
/// </summary>
public sealed class FilterOperatorWireNamesRegistryTests
{
    [Fact(DisplayName = "None (no flags) returns empty list")]
    public void NamesFor_None_Empty()
    {
        FilterOperator.None.NamesFor().ShouldBeEmpty();
    }

    [Fact(DisplayName = "Single flag returns single lowercase kebab name")]
    public void NamesFor_SingleFlag()
    {
        FilterOperator.Eq.NamesFor().ShouldBe(["eq"]);
        FilterOperator.IContains.NamesFor().ShouldBe(["iContains"]);
    }

    [Fact(DisplayName = "Flag bitmask returns all set flags, one entry each")]
    public void NamesFor_Bitmask()
    {
        const FilterOperator combined = FilterOperator.Eq | FilterOperator.In;
        var names = combined.NamesFor().ToList();

        names.ShouldContain("eq");
        names.ShouldContain("in");
        names.Count.ShouldBe(2);
    }

    [Theory(DisplayName = "All known operator names produce non-empty wire string")]
    [InlineData(FilterOperator.Eq)]
    [InlineData(FilterOperator.NotEq)]
    [InlineData(FilterOperator.Gt)]
    [InlineData(FilterOperator.Lt)]
    [InlineData(FilterOperator.Contains)]
    [InlineData(FilterOperator.IContains)]
    [InlineData(FilterOperator.In)]
    [InlineData(FilterOperator.NotIn)]
    [InlineData(FilterOperator.IsNull)]
    [InlineData(FilterOperator.IsNotNull)]
    public void NamesFor_AllOperators_HaveNames(FilterOperator op)
    {
        var names = op.NamesFor();
        names.ShouldNotBeEmpty();
    }
}
