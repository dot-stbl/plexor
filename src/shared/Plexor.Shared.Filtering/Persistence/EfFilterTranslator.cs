using System.Collections;
using System.Linq.Expressions;
using Plexor.Shared.Filtering.Operators;
using Plexor.Shared.Filtering.Parser;
using Plexor.Shared.Filtering.Registry;

namespace Plexor.Shared.Filtering.Persistence;

/// <summary>
///     Walks a neutral <see cref="FilterNode" /> tree and builds a LINQ
///     <see cref="Expression" /> for EF Core (PostgreSQL). Resolves field names
///     against <see cref="FilterableFieldSet{TEntity}" />, delegates value
///     conversion to <see cref="FilterValueConverter" />, and delegates
///     function evaluation to <see cref="FilterFunctionEvaluator" />.
/// </summary>
/// <typeparam name="TEntity">The entity type to filter.</typeparam>
/// <param name="fieldSet"></param>
internal sealed class EfFilterTranslator<TEntity>(FilterableFieldSet<TEntity> fieldSet)
{
    /// <summary>
    ///     Translates a <see cref="FilterNode" /> tree into an EF
    ///     <see cref="Expression" /> over the given <paramref name="parameter" />.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="parameter"></param>
    /// <exception cref="NotSupportedException"></exception>
    public Expression Translate(FilterNode node, ParameterExpression parameter)
    {
        return node switch
        {
            AndNode and => TranslateAnd(and, parameter),
            OrNode or => TranslateOr(or, parameter),
            ComparisonNode cmp => TranslateComparison(cmp, parameter),
            _ => throw new NotSupportedException(
                $"Unknown FilterNode type: {node.GetType().Name}")
        };
    }

    private Expression TranslateAnd(AndNode and, ParameterExpression parameter)
    {
        if (and.Children.Length == 0)
        {
            throw new InvalidOperationException("AndNode has no children.");
        }

        Expression? accumulated = null;

        foreach (var child in and.Children)
        {
            accumulated = accumulated is null
                    ? Translate(child, parameter)
                    : Expression.AndAlso(accumulated, Translate(child, parameter));
        }

        return accumulated!;
    }

    private Expression TranslateOr(OrNode or, ParameterExpression parameter)
    {
        if (or.Children.Length == 0)
        {
            throw new InvalidOperationException("OrNode has no children.");
        }

        Expression? accumulated = null;

        foreach (var child in or.Children)
        {
            accumulated = accumulated is null
                    ? Translate(child, parameter)
                    : Expression.OrElse(accumulated, Translate(child, parameter));
        }

        return accumulated!;
    }

    private Expression TranslateComparison(
        ComparisonNode cmp,
        ParameterExpression parameter)
    {
        var field = fieldSet.Find(cmp.Field)
                    ?? throw new FilterParseException($"Unknown field '{cmp.Field}'");

        if ((field.Operators & cmp.Operator) == FilterOperator.None)
        {
            throw new FilterParseException(
                $"Field '{field.Name}' does not support operator '{cmp.Operator}'");
        }

        var accessor = Expression.Property(parameter, field.Name);
        var descriptor = FilterOperatorRegistry.Get(cmp.Operator);
        var convertedValue = ConvertValue(cmp, field, descriptor.ValueKind);

        return descriptor.BuildExpression(accessor, convertedValue, field.ValueType);
    }

    /// <summary>
    ///     Converts the polymorphic <see cref="FilterValue" /> to a runtime
    ///     object suitable for the operator's expression builder. Delegates
    ///     type conversion to <see cref="FilterValueConverter" /> and function
    ///     evaluation to <see cref="FilterFunctionEvaluator" />.
    /// </summary>
    /// <param name="cmp"></param>
    /// <param name="field"></param>
    /// <param name="valueKind"></param>
    /// <exception cref="NotSupportedException"></exception>
    private static object? ConvertValue(
        ComparisonNode cmp,
        FilterableField<TEntity> field,
        ValueKind valueKind)
    {
        return valueKind switch
        {
            ValueKind.None => null,
            ValueKind.Scalar => ConvertScalarValue(cmp, field),
            ValueKind.List => ConvertListValue(cmp, field),
            _ => throw new NotSupportedException(
                $"Unknown ValueKind: {valueKind}")
        };
    }

    private static object? ConvertScalarValue(
        ComparisonNode cmp,
        FilterableField<TEntity> field)
    {
        // Pattern-match on the polymorphic FilterValue subtype.
        return cmp.Value switch
        {
            FunctionValue fn => EvaluateFunction(fn, field),

            ScalarValue scalar => FilterValueConverter.Convert(
                scalar.Raw, field.ValueType),

            NullValue => null,

            _ => throw new FilterParseException(
                $"Unexpected value type {cmp.Value.GetType().Name} " +
                $"for scalar operator on field '{cmp.Field}'")
        };
    }

    private static IList ConvertListValue(
        ComparisonNode cmp,
        FilterableField<TEntity> field)
    {
        if (cmp.Value is not ListValue list)
        {
            throw new FilterParseException(
                $"Expected a value list for IN/NotIn operator on '{cmp.Field}', " +
                $"got {cmp.Value.GetType().Name}");
        }

        // ImmutableArray<T> implements IList<T> since .NET 8 — can pass
        // directly to BuildIn which takes an IList. No allocation.
        return FilterValueConverter.ConvertList(list.Items, field.ValueType);
    }

    private static DateTimeOffset EvaluateFunction(
        FunctionValue fn,
        FilterableField<TEntity> field)
    {
        var valueType = field.ValueType;

        if (valueType != typeof(DateTimeOffset) && valueType != typeof(DateTime))
        {
            throw new FilterParseException(
                $"Function '{fn.Name}(...)' returns a timestamp and can only " +
                "be used on date/time fields.");
        }

        return FilterFunctionEvaluator.Evaluate($"{fn.Name}({fn.Argument})");
    }
}
