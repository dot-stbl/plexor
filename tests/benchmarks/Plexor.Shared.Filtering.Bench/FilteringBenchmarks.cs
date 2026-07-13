using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using Plexor.Shared.Filtering.Parser;
using Plexor.Shared.Filtering.Persistence;

namespace Plexor.Shared.Filtering.Bench;

/// <summary>
///     Benchmark suite for the filtering pipeline. Shows the difference between
///     cache-miss (full parse + expression build) and cache-hit (dictionary lookup).
///     [MemoryDiagnoser] shows allocations side-by-side.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class FilteringBenchmarks
{
    [Params(
        "name==Apple;status==Active",
        "name~Apple;(status==Active|status==Trial);createdAt>=2024-01-01",
        "(name~Apple|name~Orange|name~Banana|name~Grape|name~Mango);"
        + "(status==Active|status==Trial|status==Pending);"
        + "createdAt>=2023-01-01;createdAt<=2024-12-31;"
        + "updatedAt>=now(-30d);createdBy==admin;region==eu-west-1;"
        + "tags[]=prod,staging,dev,sandbox;priority>=3;priority<=9;"
        + "verified==true;archived==false;"
        + "description~migration;label[]=urgent,backend,core",
        "name==a|name==b|name==c|name==d|name==e|name==f|name==g|name==h|name==i|name==j"
        + "|name==k|name==l|name==m|name==n|name==o|name==p|name==q|name==r|name==s|name==t"
        + "|name==u|name==v|name==w|name==x|name==y|name==z"
        + "|name==aa|name==bb|name==cc|name==dd|name==ee|name==ff|name==gg|name==hh"
        + "|name==ii|name==jj|name==kk|name==ll|name==mm|name==nn|name==oo|name==pp"
        + "|name==qq|name==rr|name==ss|name==tt|name==uu|name==vv|name==ww")]
    public string Filter { get; set; } = string.Empty;

    // ---------- Parse (lexer + parser -> AST) ----------

    [Benchmark(Description = "Parse DSL -> AST", Baseline = true)]
    public FilterNode? Parse_ToAst()
    {
        return FilterParser.Parse(Filter);
    }

    // ---------- Expression (cold cache vs cache hit) ----------

    [Benchmark(Description = "ParseFor cold cache")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_ColdCache()
    {
        FilterExpression.ClearCache();
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    [Benchmark(Description = "ParseFor cache hit")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_CacheHit()
    {
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    // ---------- ParseFor memory allocation comparison ----------
    // Same pipeline, two paths — cold (cache miss, full build)
    // vs warm (cache hit, dict lookup). [MemoryDiagnoser] column on
    // the output table shows the difference.

    [Benchmark(Description = "ParseFor cold (emit)")]
    [BenchmarkCategory("Expression", "Memory")]
    public object? ParseFor_ColdEmit()
    {
        FilterExpression.ClearCache();
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    [Benchmark(Description = "ParseFor warm (cache hit)")]
    [BenchmarkCategory("Expression", "Memory")]
    public object? ParseFor_WarmEmit()
    {
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    // ---------- End-to-end on 100-item list ----------

    private static readonly List<BenchEntity> entities = GenerateEntities(100);

    private static List<BenchEntity> GenerateEntities(int count)
    {
        var names = new[] { "Apple", "Orange", "Banana", "Grape", "Mango", "Cherry", "Peach", "Plum" };
        var statuses = new[] { "Active", "Trial", "Pending", "Inactive" };
        var regions = new[] { "eu-west-1", "us-east-1", "ap-south-1" };
        var list = new List<BenchEntity>(count);

        for (var i = 0; i < count; i++)
        {
            list.Add(new BenchEntity
            {
                Name = names[i % names.Length],
                Status = statuses[i % statuses.Length],
                Region = regions[i % regions.Length],
                Priority = (i % 10) + 1,
                CreatedAt = new DateTimeOffset(2024, 1 + (i % 12), 1 + (i % 28), 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-(i % 30)),
                Verified = i % 3 == 0,
            });
        }

        return list;
    }

    [Benchmark(Description = "ApplyFilter 100 items (cold)")]
    public List<BenchEntity> ApplyFilter_ColdCache()
    {
        FilterExpression.ClearCache();
        return [.. entities.AsQueryable().ApplyFilter(Filter)];
    }

    [Benchmark(Description = "ApplyFilter 100 items (warm)")]
    public List<BenchEntity> ApplyFilter_CacheHit()
    {
        return [.. entities.AsQueryable().ApplyFilter(Filter)];
    }
}

public sealed class BenchEntity
{
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public int Priority { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool Verified { get; set; }
}

// ---------- Large IN-list benchmark (allocation visibility) ----------

public class InListBenchmarks
{
    /// <summary>
    /// Hard-coded short filters — [Params] requires compile-time constants
    /// (string.Join at runtime is not allowed). 1/10/100-item variants show
    /// the cost scaling; actual long IN-lists (>100) are unrealistic for the
    /// UI and would dominate benchmark time without adding insight.
    /// </summary>
    public const string Filter1 = "id[]=00000000-0000-0000-0000-000000000000";

    public const string Filter10 = "id[]=00000000-0000-0000-0000-000000000000"
        + ",id[]=00000001-0000-0000-0000-000000000000"
        + ",id[]=00000002-0000-0000-0000-000000000000"
        + ",id[]=00000003-0000-0000-0000-000000000000"
        + ",id[]=00000004-0000-0000-0000-000000000000"
        + ",id[]=00000005-0000-0000-0000-000000000000"
        + ",id[]=00000006-0000-0000-0000-000000000000"
        + ",id[]=00000007-0000-0000-0000-000000000000"
        + ",id[]=00000008-0000-0000-0000-000000000000"
        + ",id[]=00000009-0000-0000-0000-000000000000";

    [Params(Filter1, Filter10)]
    public string InListFilter { get; set; } = string.Empty;

    [Benchmark(Description = "ParseFor IN-list cold")]
    public object? ParseFor_InList_Cold()
    {
        FilterExpression.ClearCache();
        return FilterExpression.ParseFor<BenchEntity>(InListFilter);
    }

    [Benchmark(Description = "ParseFor IN-list warm")]
    public object? ParseFor_InList_Warm()
    {
        return FilterExpression.ParseFor<BenchEntity>(InListFilter);
    }
}
