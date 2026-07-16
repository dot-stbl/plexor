namespace Plexor.Shared.Filtering.Registry;

/// <summary>
///     Provides filterable field metadata for split-query requests. Implemented by the
///     Reporting module (reads <c>[Dimension]</c> / <c>[Metric]</c> attributes from
///     table models) and consumed by <c>SplitFilterSchemaTransformer</c> in
///     Hybrid.Composition to enrich the OpenAPI <c>SplitQueryRequest</c> schema with
///     <c>x-filterable</c> extension. Lives in Shared.Filtering so neither
///     Hybrid.Composition nor Hybrid.Shared.Contracts need a dependency on Reporting.
/// </summary>
public interface ISplitFilterFieldsProvider
{
    /// <summary>
    ///     Returns every filterable field (dimension + metric aliases with their
    ///     operators and types) available across all registered ClickHouse table models.
    /// </summary>
    public IReadOnlyList<SplitFilterField> GetFields();
}

/// <summary>
///     One filterable field in a split-query filter.
/// </summary>
/// <param name="Name">The FE-facing alias (e.g. <c>"Banner"</c>, <c>"ctr"</c>).</param>
/// <param name="ClrType">
///     CLR type name for FE codegen (<c>"String"</c>, <c>"Int64"</c>, <c>"Decimal"</c>,
///     <c>"DateTime"</c>).
/// </param>
/// <param name="Operators">DSL operator names this field accepts (<c>"eq"</c>, <c>"gt"</c>, <c>"in"</c>, ...).</param>
public sealed record SplitFilterField(
    string Name,
    string ClrType,
    IReadOnlyList<string> Operators);

/// <summary>
///     No-op default. Registered when the Reporting module is not wired (e.g. in
///     unit-test hosts). Produces an empty field list — the OpenAPI schema simply
///     omits the <c>x-filterable</c> extension.
/// </summary>
public sealed class NullSplitFilterFieldsProvider : ISplitFilterFieldsProvider
{
    /// <inheritdoc />
    public IReadOnlyList<SplitFilterField> GetFields()
    {
        return [];
    }
}
