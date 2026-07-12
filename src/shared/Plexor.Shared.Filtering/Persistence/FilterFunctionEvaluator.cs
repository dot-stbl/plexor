using Plexor.Shared.Filtering.Parser;

namespace Plexor.Shared.Filtering.Persistence;

/// <summary>
///     Evaluates filter DSL function calls (e.g. <c>now(-7d)</c>) into typed
///     runtime values. Separated from <see cref="EfFilterTranslator{TEntity}" />
///     so the translator only walks the AST — function evaluation is a
///     separate responsibility.
/// </summary>
public static class FilterFunctionEvaluator
{
    /// <summary>
    ///     Evaluates a raw function-call string (e.g. <c>"now(-7d)"</c>) against
    ///     the current UTC clock. Returns a <see cref="DateTimeOffset" />.
    /// </summary>
    /// <param name="call">Raw function-call text from the parser.</param>
    /// <param name="position">Source position for error messages.</param>
    /// <exception cref="FilterParseException">Unknown function, malformed
    /// argument, or the function returns a type incompatible with the field.</exception>
    public static DateTimeOffset Evaluate(string call, int position = 0)
    {
        var parts = call.TrimEnd(')').Split('(', 2);
        var functionName = parts[0];
        var argument = parts.Length > 1 ? parts[1] : string.Empty;

        return FilterFunctions.EvaluateNow(functionName, argument, position);
    }
}
