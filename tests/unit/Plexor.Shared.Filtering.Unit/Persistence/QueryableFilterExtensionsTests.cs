using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Persistence;

/// <summary>
///     Tests for <see cref="QueryableFilterExtensions" /> — the IQueryable
///     glue that ties the parser + translator + cache to a real LINQ pipeline.
///     Uses tiny in-memory lists to exercise the full Apply path.
/// </summary>
public sealed class QueryableFilterExtensionsTests
{
    [Fact(DisplayName = "ApplyFilter with null source returns source unchanged")]
    public void ApplyFilter_NullSource_PassThrough()
    {
        var source = new List<TestEntity>().AsQueryable();

        var result = source.ApplyFilter(null).ToList();

        result.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ApplyFilter with empty string returns source unchanged")]
    public void ApplyFilter_EmptyString_PassThrough()
    {
        var source = new List<TestEntity>().AsQueryable();

        var result = source.ApplyFilter("").ToList();

        result.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ApplyFilter on in-memory list filters by Eq operator")]
    public void ApplyFilter_SimpleFilter()
    {
        var entities = new List<TestEntity>
        {
            new() { Id = 1, Name = "Apple", Status = "Active" },
            new() { Id = 2, Name = "Orange", Status = "Active" },
            new() { Id = 3, Name = "Banana", Status = "Inactive" },
        };

        var result = entities.AsQueryable()
            .ApplyFilter("status==Active")
            .ToList();

        result.Count.ShouldBe(2);
        result.Select(static e => e.Name).ShouldBe(["Apple", "Orange"], ignoreOrder: true);
    }

    [Fact(DisplayName = "ApplyFilter with case-insensitive Contains (~)")]
    public void ApplyFilter_ContainsCaseInsensitive()
    {
        var entities = new List<TestEntity>
        {
            new() { Id = 1, Name = "Apple" },
            new() { Id = 2, Name = "apricot" },
            new() { Id = 3, Name = "Banana" },
        };

        var result = entities.AsQueryable()
            .ApplyFilter("name~ap")
            .ToList();

        result.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "ApplyFilter with IN list ([]=)")]
    public void ApplyFilter_InList()
    {
        var entities = new List<TestEntity>
        {
            new() { Id = 1, Name = "Apple" },
            new() { Id = 2, Name = "Orange" },
            new() { Id = 3, Name = "Banana" },
        };

        var result = entities.AsQueryable()
            .ApplyFilter("id[]=1,3")
            .ToList();

        result.Count.ShouldBe(2);
        result.Select(static e => e.Id).ShouldBe([1, 3], ignoreOrder: true);
    }

    [Fact(DisplayName = "ApplyFilter with OR chain")]
    public void ApplyFilter_OrChain()
    {
        var entities = new List<TestEntity>
        {
            new() { Id = 1, Name = "Apple" },
            new() { Id = 2, Name = "Orange" },
            new() { Id = 3, Name = "Banana" },
        };

        var result = entities.AsQueryable()
            .ApplyFilter("name==Apple|name==Orange")
            .ToList();

        result.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "ApplyFilter second call hits cache (same result)")]
    public void ApplyFilter_CacheHit()
    {
        var entities = new List<TestEntity>
        {
            new() { Id = 1, Name = "Apple" },
            new() { Id = 2, Name = "Orange" },
        };

        var first = entities.AsQueryable().ApplyFilter("name~A").ToList();
        var second = entities.AsQueryable().ApplyFilter("name~A").ToList();

        first.Count.ShouldBe(second.Count);
        first.Select(static e => e.Id).ShouldBe(second.Select(static e => e.Id));
    }

    [Fact(DisplayName = "ApplyFilter custom field set is honored")]
    public void ApplyFilter_CustomFieldSet()
    {
        var entities = new List<TestEntity>
        {
            new() { Id = 1, Name = "Apple" },
        };

        var fields = FilterableFieldRegistry.For<TestEntity>();

        var result = entities.AsQueryable()
            .ApplyFilter("name==Apple", fields)
            .ToList();

        result.Count.ShouldBe(1);
    }
}

public sealed class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
