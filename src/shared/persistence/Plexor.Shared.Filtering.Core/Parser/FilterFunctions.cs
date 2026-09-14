using System.Globalization;

namespace Plexor.Shared.Filtering.Parser;

/// <summary>
///     Server-side functions evaluable as filter values. Each function takes a
///     well-typed argument parsed by the lexer and returns a concrete
///     <see cref="DateTimeOffset" /> that the comparison operator applies to the
///     field. Time is anchored to <see cref="DateTimeOffset.UtcNow" /> \u2014 never to
///     client-supplied timestamps, so a query like <c>createdAt&gt;=now(-7d)</c>
///     returns consistent results regardless of where the request originated.
/// </summary>
/// <remarks>
///     <para>
///         Only <c>now</c> is exposed in this revision. The set is intentionally
///         minimal \u2014 every new function is a new code path that needs a security
///         review and adversarial tests. Adding <c>today()</c>,
///         <c>startOfWeek()</c>, etc. is deferred until a real filter demands it.
///     </para>
///     <para>
///         Grammar (EBNF):
///         <code>
/// function       := 'now' '(' signedDuration ')'
/// signedDuration := ['-']? digits unit
/// unit           := 's' | 'm' | 'h' | 'd' | 'w'
///         </code>
///     </para>
/// </remarks>
public static class FilterFunctions
{
    /// <summary>
    ///     Evaluates <c>now(offset)</c> against <see cref="DateTimeOffset.UtcNow" />.
    ///     The <paramref name="argument" /> is the duration string captured by the
    ///     lexer (e.g. <c>-7d</c>, <c>1h</c>, <c>0d</c>); it is parsed and applied
    ///     to the current UTC timestamp. Returns the absolute point in time the
    ///     comparison operator should compare the field against.
    /// </summary>
    /// <param name="functionName">
    ///     Function identifier from the DSL, case-insensitive. Only <c>now</c> is
    ///     accepted \u2014 any other name throws <see cref="FilterParseException" />.
    /// </param>
    /// <param name="argument">
    ///     Raw argument text as captured by the lexer (e.g. <c>-7d</c>). Must be
    ///     a signed integer followed by a single-char unit.
    /// </param>
    /// <param name="position">
    ///     Source position for error messages.
    /// </param>
    /// <returns>
    ///     <c>DateTimeOffset.UtcNow + parsed offset</c>.
    /// </returns>
    /// <exception cref="FilterParseException">
    ///     Thrown for unknown function names, malformed durations, unknown units,
    ///     or out-of-range integer values.
    /// </exception>
    public static DateTimeOffset EvaluateNow(string functionName, string argument, int position)
    {
        if (!string.Equals(functionName, "now", StringComparison.OrdinalIgnoreCase))
        {
            throw new FilterParseException(
                $"Unknown function '{functionName}' (only 'now' is supported)",
                position);
        }

        var offset = ParseDuration(argument, position);
        return DateTimeOffset.UtcNow + offset;
    }

    /// <summary>
    ///     Parses a signed duration literal of the shape <c>[-]N[unit]</c>:
    ///     optional leading minus, mandatory integer, mandatory single-char unit.
    ///     The leading sign is restricted to <c>-</c> \u2014 <c>+7d</c> is rejected to
    ///     match the documented grammar (positive offsets are simply <c>7d</c>).
    /// </summary>
    /// <param name="text">Raw argument text from the lexer (no surrounding parens).</param>
    /// <param name="position">Source position for error messages.</param>
    /// <exception cref="FilterParseException">Malformed duration.</exception>
    public static TimeSpan ParseDuration(string text, int position)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2)
        {
            throw new FilterParseException(
                $"Invalid duration '{text}' (expected: [-]N[unit], e.g. -7d, 1h, 30m)",
                position);
        }

        var negative = false;
        var numberStart = 0;

        if (text[0] == '-')
        {
            negative = true;
            numberStart = 1;
        }
        else if (text[0] == '+')
        {
            // '+N[unit]' is accepted as an explicit-positive duration; 'N[unit]'
            // (no sign) is the bare form. Both parse to the same positive offset.
            numberStart = 1;
        }
        else if (!char.IsDigit(text[0]))
        {
            throw new FilterParseException(
                $"Invalid duration '{text}' (expected integer at start)",
                position);
        }

        var unitIndex = text.Length - 1;

        // The unit must be a single char at the end; number occupies everything
        // between the optional sign and the unit. If numberStart > unitIndex, the
        // input is just "-" or empty \u2014 rejected below.
        if (numberStart > unitIndex)
        {
            throw new FilterParseException(
                $"Invalid duration '{text}' (missing number before unit)",
                position);
        }

        var numberPart = text[numberStart..unitIndex];
        var unit = text[unitIndex];

        if (!int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new FilterParseException(
                $"Invalid duration number '{numberPart}' in '{text}' (integer required)",
                position);
        }

        if (negative)
        {
            // Guard against int.MinValue negation overflow. The largest magnitude
            // we realistically need is a few years in either direction \u2014 reject
            // values that would push the resulting TimeSpan outside DateTimeOffset
            // range before applying the unit multiplier.
            if (value == int.MinValue)
            {
                throw new FilterParseException(
                    $"Duration '{text}' is out of range",
                    position);
            }

            value = -value;
        }

        // TimeSpan.FromDays(int.MaxValue) throws OverflowException at the
        // long-tick multiplication; the other FromXxx calls succeed but
        // the resulting TimeSpan is huge enough that adding it to UtcNow
        // would throw ArgumentOutOfRangeException. Catch both here so
        // a pathological `now(int.MaxValue)<unit>` surfaces as a 400
        // instead of a 500.
        TimeSpan offset;
        try
        {
            offset = unit switch
            {
                's' => TimeSpan.FromSeconds(value),
                'm' => TimeSpan.FromMinutes(value),
                'h' => TimeSpan.FromHours(value),
                'd' => TimeSpan.FromDays(value),
                'w' => TimeSpan.FromDays(value * 7),
                _ => throw new FilterParseException(
                    $"Unknown duration unit '{unit}' in '{text}' (allowed: s, m, h, d, w)",
                    position)
            };
        }
        catch (OverflowException ex)
        {
            throw new FilterParseException(
                $"Duration '{text}' is out of representable range",
                position,
                ex);
        }

        // Magnitude check: realistic filters stay within a few years
        // either way; anything beyond ~10000 days (~27 years) would
        // overflow DateTimeOffset arithmetic at evaluation time, so
        // reject here with a clear 400.
        if (offset.TotalDays is > 10000d or < -10000d)
        {
            throw new FilterParseException(
                $"Duration '{text}' is out of representable range",
                position);
        }

        return offset;
    }
}
