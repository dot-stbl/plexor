using System.Collections;
using System.Globalization;
using Plexor.Shared.Filtering.Parser;

namespace Plexor.Shared.Filtering.Persistence;

/// <summary>
///     Converts raw string values from the filter DSL to the field's CLR type.
///     Registry-based — adding a new convertible type is one entry in the
///     <see cref="converters" /> dictionary, not a new arm in a switch statement.
/// </summary>
/// <remarks>
///     <para><b>Why registry, not switch.</b> The old <c>EfFilterTranslator</c>
///     had a 7-arm <c>switch (underlying)</c> for type conversion. Adding
///     <c>DateOnly</c>, <c>TimeOnly</c>, or a custom value object required
///     editing the translator's source. Now it's one line:</para>
///     <code>
/// FilterValueConverter.Register&lt;DateOnly&gt;(static s => DateOnly.Parse(s, CultureInfo.InvariantCulture));
///     </code>
/// </remarks>
public static class FilterValueConverter
{
    private static readonly Dictionary<Type, Func<string, object>> converters = BuildDefaultConverters();

    /// <summary>
    ///     Converts <paramref name="text" /> to the CLR type
    ///     <paramref name="targetType" /> (or its nullable underlying).
    ///     Throws <see cref="FilterParseException" /> on conversion failure.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="targetType"></param>
    /// <exception cref="FilterParseException"></exception>
    public static object Convert(string text, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (converters.TryGetValue(underlying, out var converter))
        {
            try
            {
                return converter(text);
            }
            catch (Exception ex) when (ex is not FilterParseException)
            {
                throw new FilterParseException(
                    $"Cannot convert '{text}' to {underlying.Name}", 0, ex);
            }
        }

        if (underlying.IsEnum)
        {
            try
            {
                return Enum.Parse(underlying, text, true);
            }
            catch (Exception ex)
            {
                throw new FilterParseException(
                    $"Cannot convert '{text}' to enum {underlying.Name}", 0, ex);
            }
        }

        // Fallback: single-string-constructor on the target type. This is the
        // contract for custom value types without an explicit Register —
        // e.g. `record MyCustomType(string Value)` synthesises a primary
        // constructor that takes the raw string verbatim. Without this
        // fallback the DSL can't deserialize custom types until the caller
        // explicitly opts in via Register<T>.
        var stringCtor = underlying.GetConstructor([typeof(string)]);

        if (stringCtor is not null)
        {
            return stringCtor.Invoke([text]);
        }

        // Fallback: IConvertible.
        try
        {
            return System.Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new FilterParseException(
                $"Cannot convert '{text}' to {underlying.Name}", 0, ex);
        }
    }

    /// <summary>
    ///     Registers (or replaces) a converter for <typeparamref name="T" />.
    ///     Call at startup — before any filter query hits the pipeline.
    /// </summary>
    /// <typeparam name="T">The target CLR type.</typeparam>
    /// <param name="parse">Function that converts a raw string to <typeparamref name="T" />.</param>
    public static void Register<T>(Func<string, T> parse)
        where T : notnull
    {
        converters[typeof(T)] = text => parse(text);
    }

    /// <summary>Builds a typed <see cref="IList" /> from raw string values.</summary>
    /// <param name="strings"></param>
    /// <param name="elementType"></param>
    public static IList ConvertList(IReadOnlyList<string> strings, Type elementType)
    {
        var underlying = Nullable.GetUnderlyingType(elementType) ?? elementType;
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(underlying))!;

        foreach (var s in strings)
        {
            list.Add(Convert(s, underlying));
        }

        return list;
    }

    private static Dictionary<Type, Func<string, object>> BuildDefaultConverters()
    {
        return new Dictionary<Type, Func<string, object>>
        {
            [typeof(string)] = static s => s,

            [typeof(int)] = static s => int.Parse(s, CultureInfo.InvariantCulture),
            [typeof(long)] = static s => long.Parse(s, CultureInfo.InvariantCulture),
            [typeof(short)] = static s => short.Parse(s, CultureInfo.InvariantCulture),
            [typeof(byte)] = static s => byte.Parse(s, CultureInfo.InvariantCulture),

            [typeof(uint)] = static s => uint.Parse(s, CultureInfo.InvariantCulture),
            [typeof(ulong)] = static s => ulong.Parse(s, CultureInfo.InvariantCulture),
            [typeof(ushort)] = static s => ushort.Parse(s, CultureInfo.InvariantCulture),
            [typeof(sbyte)] = static s => sbyte.Parse(s, CultureInfo.InvariantCulture),

            [typeof(float)] = static s => float.Parse(s, CultureInfo.InvariantCulture),
            [typeof(double)] = static s => double.Parse(s, CultureInfo.InvariantCulture),
            [typeof(decimal)] = static s => decimal.Parse(s, CultureInfo.InvariantCulture),

            [typeof(bool)] = static s => bool.Parse(s),

            [typeof(DateTimeOffset)] = static s => DateTimeOffset.Parse(
                s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),

            [typeof(DateTime)] = static s => DateTime.Parse(
                s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),

            [typeof(TimeSpan)] = static s => TimeSpan.Parse(
                s, CultureInfo.InvariantCulture),

            [typeof(Guid)] = static s => Guid.Parse(s),

            [typeof(DateOnly)] = static s => DateOnly.Parse(
                s, CultureInfo.InvariantCulture),

            [typeof(TimeOnly)] = static s => TimeOnly.Parse(
                s, CultureInfo.InvariantCulture),
        };
    }
}
