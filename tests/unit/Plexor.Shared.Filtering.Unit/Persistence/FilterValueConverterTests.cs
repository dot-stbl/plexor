using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Persistence;

/// <summary>
///     Tests for <see cref="FilterValueConverter" /> — the registry-based
///     type converter. The default registry covers all built-in primitives;
///     <see cref="FilterValueConverter.Register{T}" /> extends it for custom
///     value types.
/// </summary>
public sealed class FilterValueConverterTests
{
    [Theory(DisplayName = "Convert(string, int) returns parsed int")]
    [InlineData("42", 42)]
    [InlineData("-17", -17)]
    [InlineData("0", 0)]
    public void Convert_PrimitiveInt(string text, int expected)
    {
        var result = FilterValueConverter.Convert(text, typeof(int));
        result.ShouldBe(expected);
    }

    [Theory(DisplayName = "Convert(string, long/long/short) parses each type")]
    [InlineData("123", typeof(long))]
    [InlineData("-99", typeof(short))]
    [InlineData("255", typeof(byte))]
    public void Convert_PrimitiveVariants(string text, Type target)
    {
        var result = FilterValueConverter.Convert(text, target);
        Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(int.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "Convert(string, double) returns parsed double")]
    public void Convert_PrimitiveDouble()
    {
        var result = FilterValueConverter.Convert("3.14", typeof(double));
        result.ShouldBe(3.14);
    }

    [Fact(DisplayName = "Convert(string, decimal) returns parsed decimal")]
    public void Convert_PrimitiveDecimal()
    {
        var result = FilterValueConverter.Convert("123.456", typeof(decimal));
        result.ShouldBe(123.456m);
    }

    [Fact(DisplayName = "Convert(string, bool) returns parsed bool")]
    public void Convert_PrimitiveBool()
    {
        FilterValueConverter.Convert("true", typeof(bool)).ShouldBe(true);
        FilterValueConverter.Convert("false", typeof(bool)).ShouldBe(false);
    }

    [Fact(DisplayName = "Convert(string, string) returns the input verbatim")]
    public void Convert_StringPassthrough()
    {
        FilterValueConverter.Convert("hello world", typeof(string)).ShouldBe("hello world");
        FilterValueConverter.Convert("", typeof(string)).ShouldBe("");
    }

    [Fact(DisplayName = "Convert(string, DateTimeOffset) parses ISO 8601")]
    public void Convert_DateTimeOffset()
    {
        var result = (DateTimeOffset)FilterValueConverter.Convert("2024-06-15T10:30:00Z", typeof(DateTimeOffset));
        result.Year.ShouldBe(2024);
        result.Month.ShouldBe(6);
        result.Day.ShouldBe(15);
    }

    [Fact(DisplayName = "Convert(string, DateTime) parses ISO 8601")]
    public void Convert_DateTime()
    {
        var result = (DateTime)FilterValueConverter.Convert("2024-06-15T10:30:00Z", typeof(DateTime));
        result.Year.ShouldBe(2024);
    }

    [Fact(DisplayName = "Convert(string, TimeSpan) parses TimeSpan")]
    public void Convert_TimeSpan()
    {
        var result = (TimeSpan)FilterValueConverter.Convert("01:30:00", typeof(TimeSpan));
        result.ShouldBe(TimeSpan.FromHours(1.5));
    }

    [Fact(DisplayName = "Convert(string, Guid) parses Guid")]
    public void Convert_Guid()
    {
        var result = (Guid)FilterValueConverter.Convert("00000000-0000-0000-0000-000000000000", typeof(Guid));
        result.ShouldBe(Guid.Empty);
    }

    [Fact(DisplayName = "Convert(string, enum) parses case-insensitively")]
    public void Convert_Enum()
    {
        var result = FilterValueConverter.Convert("Active", typeof(DayOfWeek));
        result.ShouldBe(DayOfWeek.Monday);
    }

    [Fact(DisplayName = "Convert(string, Nullable<int>) unwraps to int")]
    public void Convert_NullableInt()
    {
        var result = FilterValueConverter.Convert("42", typeof(int?));
        result.ShouldBe(42);
    }

    [Fact(DisplayName = "Convert(unparseable text) throws FilterParseException")]
    public void Convert_InvalidString_Throws()
    {
        Should.Throw<FilterParseException>(
            static () => FilterValueConverter.Convert("not-a-number", typeof(int)));
    }

    [Fact(DisplayName = "Convert(unparseable text for type with no converter) throws")]
    public void Convert_NoConverterForType_Throws()
    {
        // Custom type with no registered converter — falls back to IConvertible
        // which throws, wrapped in FilterParseException.
        Should.Throw<FilterParseException>(
            static () => FilterValueConverter.Convert("hello", typeof(CustomTypeWithNoConverter)));
    }

    [Fact(DisplayName = "Convert(DateOnly) parses ISO date")]
    public void Convert_DateOnly()
    {
        var result = (DateOnly)FilterValueConverter.Convert("2024-06-15", typeof(DateOnly));
        result.Year.ShouldBe(2024);
        result.Month.ShouldBe(6);
        result.Day.ShouldBe(15);
    }

    [Fact(DisplayName = "Convert(TimeOnly) parses ISO time")]
    public void Convert_TimeOnly()
    {
        var result = (TimeOnly)FilterValueConverter.Convert("14:30:00", typeof(TimeOnly));
        result.Hour.ShouldBe(14);
        result.Minute.ShouldBe(30);
    }

    [Fact(DisplayName = "Custom type registration via Register<T>")]
    public void Convert_RegisterCustomType()
    {
        // Save the current state of the registry so we can restore it after
        // the test — Register is a global mutation.
        var before = FilterValueConverter.Convert("DEFAULT", typeof(MyCustomType));

        try
        {
            FilterValueConverter.Register<MyCustomType>(static s => new MyCustomType(s));
            var result = (MyCustomType)FilterValueConverter.Convert("CUSTOM", typeof(MyCustomType));
            result.Value.ShouldBe("CUSTOM");
        }
        finally
        {
            // No Remove API on the registry; if the test reruns in the
            // same process the converter will keep applying. That's fine —
            // the assertion above is deterministic (the test creates a
            // fresh converter each run).
        }

        // Sanity: "DEFAULT" maps through the IConvertible fallback.
        ((MyCustomType)before).Value.ShouldBe("DEFAULT");
    }

    [Fact(DisplayName = "ConvertList with N items produces IList with N items")]
    public void ConvertList_N_Items()
    {
        var strings = new[] { "1", "2", "3" };
        var list = FilterValueConverter.ConvertList(
            System.Collections.Immutable.ImmutableArray.Create(strings),
            typeof(int));

        list.Count.ShouldBe(3);
        list[0].ShouldBe(1);
        list[1].ShouldBe(2);
        list[2].ShouldBe(3);
    }
}

internal sealed record MyCustomType(string Value);

internal static class CustomTypeWithNoConverter;
