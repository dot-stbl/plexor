using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using Plexor.Shared.Filtering;

namespace Plexor.Shared.Filtering.Bench;

/// <summary>
///     Benchmark suite for the filtering pipeline. Compares cache-miss
///     (full parse + expression build) vs cache-hit (dictionary lookup).
///     Memory column shows the difference in allocations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public sealed class FilteringBenchmarks
{
    [Params(
        "name==Apple;status==Active",
        "name~Apple;(status==Active|status==Trial);createdAt>=2024-01-01",
        "(name~Apple|name~Orange|name~Banana);status==Active;createdAt>=now(-7d)")]
    public string Filter { get; set; } = string.Empty;

    // ---------- Parse (lexer + parser -> AST) ----------

    [Benchmark(Description = "Parse DSL -> AST", Baseline = true)]
    [BenchmarkCategory("Parse")]
    public FilterNode? Parse_ToAst()
    {
        return FilterParser.Parse(Filter);
    }

    // ---------- Expression build (AST -> Expression) ----------

    [Benchmark(Description = "ParseFor (cold cache)")]
    [BenchmarkCategory("Expression")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_ColdCache()
    {
        FilterExpression.ClearCache();
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    [Benchmark(Description = "ParseFor (cache hit)")]
    [BenchmarkCategory("Expression")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_CacheHit()
    {
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    // ---------- End-to-end on in-memory list ----------

    private static readonly List<BenchEntity> entities =
    [
        new BenchEntity { Name = "Apple", Status = "Active", CreatedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero) },
        new BenchEntity { Name = "Orange", Status = "Trial", CreatedAt = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero) },
        new BenchEntity { Name = "Banana", Status = "Active", CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) },
        new BenchEntity { Name = "Grape", Status = "Inactive", CreatedAt = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero) },
        new BenchEntity { Name = "Mango", Status = "Active", CreatedAt = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero) },
    ];

    [Benchmark(Description = "ApplyFilter 5 items (cold)")]
    [BenchmarkCategory("EndToEnd")]
    public List<BenchEntity> ApplyFilter_ColdCache()
    {
        FilterExpression.ClearCache();
        return entities.AsQueryable().ApplyFilter(Filter).ToList();
    }

    [Benchmark(Description = "ApplyFilter 5 items (warm)")]
    [BenchmarkCategory("EndToEnd")]
    public List<BenchEntity> ApplyFilter_CacheHit()
    {
        return entities.AsQueryable().ApplyFilter(Filter).ToList();
    }
}

/// <summary>
///     Minimal entity for benchmarking. Mirrors a typical filterable record
///     (name + status + timestamp). Public properties the registry reflects on.
/// </summary>
public sealed class BenchEntity
{
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
