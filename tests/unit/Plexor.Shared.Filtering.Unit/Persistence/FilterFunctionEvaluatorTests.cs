using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Persistence;

/// <summary>
///     Tests for <see cref="FilterFunctionEvaluator" /> — the date/time
///     function evaluator split out from <c>EfFilterTranslator</c> in the
///     polymorphic-value refactor.
/// </summary>
public sealed class FilterFunctionEvaluatorTests
{
    [Fact(DisplayName = "now(-7d) returns approximately 7 days ago")]
    public void Evaluate_NowMinus7Days()
    {
        var before = DateTimeOffset.UtcNow;
        var result = FilterFunctionEvaluator.Evaluate("now(-7d)");
        var after = DateTimeOffset.UtcNow;

        // The result must be between (now-7d) computed before vs after the
        // call. Generous bounds: 6.5 to 7.5 days.
        var expected = before.AddDays(-7);
        result.ShouldBeGreaterThanOrEqualTo(expected.AddHours(-12));
        result.ShouldBeLessThanOrEqualTo(after.AddDays(-7).AddHours(12));
    }

    [Fact(DisplayName = "now(0d) returns approximately now")]
    public void Evaluate_NowZero()
    {
        var before = DateTimeOffset.UtcNow;
        var result = FilterFunctionEvaluator.Evaluate("now(0d)");
        var after = DateTimeOffset.UtcNow;

        result.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-5));
        result.ShouldBeLessThanOrEqualTo(after.AddSeconds(5));
    }

    [Fact(DisplayName = "now(+1h) returns approximately 1 hour in the future")]
    public void Evaluate_NowPlus1Hour()
    {
        var before = DateTimeOffset.UtcNow;
        var result = FilterFunctionEvaluator.Evaluate("now(+1h)");

        result.ShouldBeGreaterThanOrEqualTo(before.AddHours(1).AddSeconds(-5));
    }

    [Fact(DisplayName = "Unknown function name throws FilterParseException")]
    public void Evaluate_UnknownFunction_Throws()
    {
        Should.Throw<FilterParseException>(
            static () => FilterFunctionEvaluator.Evaluate("today(-1d)"));
    }

    [Fact(DisplayName = "Malformed duration throws FilterParseException")]
    public void Evaluate_MalformedDuration_Throws()
    {
        Should.Throw<FilterParseException>(
            static () => FilterFunctionEvaluator.Evaluate("now(abc)"));
    }
}
