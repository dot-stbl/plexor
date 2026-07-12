using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using Plexor.Shared.Filtering;

namespace Plexor.Shared.Filtering.Bench;

/// <summary>
///     Benchmark suite for the filtering pipeline. Covers the full range
///     from tiny 2-clause filters to adversarial 50+ clause expressions.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public sealed class FilteringBenchmarks
{
    // ---------- Small filters (typical list-page) ----------

    [ParamsSource(nameof(SmallFilters))]
    public string Filter { get; set; } = string.Empty;

    public static IEnumerable<string> SmallFilters =>
    [
        "name==Apple",
        "name==Apple;status==Active",
        "name~Apple;status==Active;createdAt>=2024-01-01",
    ];

    // ---------- Medium filters (dashboard, multi-condition) ----------

    [ParamsSource(nameof(MediumFilters))]
    public string MediumFilter { get; set; } = string.Empty;

    public static IEnumerable<string> MediumFilters =>
    [
        // 5-clause AND chain
        "name~Apple;status==Active;createdAt>=2024-01-01;updatedAt>=now(-7d);createdBy==admin",
        // OR group + AND
        "(name~Apple|name~Orange|name~Banana);status==Active;createdAt>=now(-7d)",
        // Nested OR/AND with IN-list
        "(status==Active|status==Trial);tags[]=prod,staging,dev;region==eu-west-1;team~platform",
    ];

    // ---------- Large filters (bulk API, power-user search) ----------

    [ParamsSource(nameof(LargeFilters))]
    public string LargeFilter { get; set; } = string.Empty;

    public static IEnumerable<string> LargeFilters =>
    [
        // 10-clause AND with OR groups
        "(name~Apple|name~Orange);status==Active;createdAt>=2024-01-01;updatedAt>=now(-1h);"
        + "createdBy==admin;region==eu-west-1;tags[]=prod,staging;priority>=5;assignee==dev-team-1;verified==true",

        // 20-clause — extreme power-user search
        "(name~Apple|name~Orange|name~Banana|name~Grape|name~Mango);"
        + "(status==Active|status==Trial|status==Pending);"
        + "createdAt>=2023-01-01;createdAt<=2024-12-31;"
        + "updatedAt>=now(-30d);createdBy==admin;updatedBy==admin;"
        + "region==eu-west-1;zone==a;tags[]=prod,staging,dev,sandbox;"
        + "priority>=3;priority<=9;costCenter==eng-platform;"
        + "assignee==dev-team-1;verified==true;archived==false;"
        + "description~migration;label[]=urgent,backend,core",

        // Deep nesting — 6 levels of parens
        "(((((name~Apple)));status==Active));"
        + "(createdAt>=2024-01-01|(updatedAt>=now(-1h)&verified==true));"
        + "tags[]=prod,staging,dev,sandbox,qa,ci,cd,monitoring,alerts,on-call",
    ];

    // ---------- Adversarial filters (worst-case parse complexity) ----------

    [ParamsSource(nameof(AdversarialFilters))]
    public string AdversarialFilter { get; set; } = string.Empty;

    public static IEnumerable<string> AdversarialFilters =>
    [
        // 50 OR clauses — O(n) parser, 50 allocations for OrNode children
        "name==a|name==b|name==c|name==d|name==e|name==f|name==g|name==h|name==i|name==j"
        + "|name==k|name==l|name==m|name==n|name==o|name==p|name==q|name==r|name==s|name==t"
        + "|name==u|name==v|name==w|name==x|name==y|name==z"
        + "|name==aa|name==bb|name==cc|name==dd|name==ee|name==ff|name==gg|name==hh"
        + "|name==ii|name==jj|name==kk|name==ll|name==mm|name==nn|name==oo|name==pp"
        + "|name==qq|name==rr|name==ss|name==tt|name==uu|name==vv|name==ww",

        // 50 IN-list entries — O(n) list builder, 50 string conversions
        "id[]=" + string.Join(",", Enumerable.Range(0, 50).Select(static i => $"0000000{i:000}-0000-0000-0000-000000000000")),

        // Near-max token count (256 limit) — 50 AND clauses × ~5 tokens each
        string.Join(";", Enumerable.Range(0, 50).Select(static i => $"field{i:000}==value{i:000}")),
    ];

    // ==================== Benchmarks ====================

    // ---------- Parse (lexer + parser -> AST) ----------

    [Benchmark(Description = "Parse small", Baseline = true)]
    [BenchmarkCategory("Parse", "Small")]
    public FilterNode? Parse_Small()
    {
        return FilterParser.Parse(Filter);
    }

    [Benchmark(Description = "Parse medium")]
    [BenchmarkCategory("Parse", "Medium")]
    public FilterNode? Parse_Medium()
    {
        return FilterParser.Parse(MediumFilter);
    }

    [Benchmark(Description = "Parse large")]
    [BenchmarkCategory("Parse", "Large")]
    public FilterNode? Parse_Large()
    {
        return FilterParser.Parse(LargeFilter);
    }

    [Benchmark(Description = "Parse adversarial")]
    [BenchmarkCategory("Parse", "Adversarial")]
    public FilterNode? Parse_Adversarial()
    {
        return FilterParser.Parse(AdversarialFilter);
    }

    // ---------- Expression build (cache miss vs hit) ----------

    [Benchmark(Description = "ParseFor small (cold)")]
    [BenchmarkCategory("Expression", "Small")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_Small_Cold()
    {
        FilterExpression.ClearCache();
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    [Benchmark(Description = "ParseFor small (warm)")]
    [BenchmarkCategory("Expression", "Small")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_Small_Warm()
    {
        return FilterExpression.ParseFor<BenchEntity>(Filter);
    }

    [Benchmark(Description = "ParseFor medium (cold)")]
    [BenchmarkCategory("Expression", "Medium")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_Medium_Cold()
    {
        FilterExpression.ClearCache();
        return FilterExpression.ParseFor<BenchEntity>(MediumFilter);
    }

    [Benchmark(Description = "ParseFor medium (warm)")]
    [BenchmarkCategory("Expression", "Medium")]
    public Expression<Func<BenchEntity, bool>>? ParseFor_Medium_Warm()
    {
        return FilterExpression.ParseFor<BenchEntity>(MediumFilter);
    }

    // ---------- End-to-end on in-memory list ----------

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
    [BenchmarkCategory("EndToEnd", "Small")]
    public List<BenchEntity> ApplyFilter_100_Cold()
    {
        FilterExpression.ClearCache();
        return entities.AsQueryable().ApplyFilter(Filter).ToList();
    }

    [Benchmark(Description = "ApplyFilter 100 items (warm)")]
    [BenchmarkCategory("EndToEnd", "Small")]
    public List<BenchEntity> ApplyFilter_100_Warm()
    {
        return entities.AsQueryable().ApplyFilter(Filter).ToList();
    }
}

/// <summary>
///     Minimal entity for benchmarking. Mirrors a typical filterable record.
/// </summary>
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
