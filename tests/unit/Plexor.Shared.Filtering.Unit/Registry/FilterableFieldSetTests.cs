using System.ComponentModel.DataAnnotations.Schema;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Registry;

/// <summary>
///     Tests for <see cref="FilterableFieldSet{TEntity}" /> — the
///     reflection-based field registry. Every public instance property
///     of the entity type is filterable unless marked <c>[NotMapped]</c> or its
///     CLR type maps to <see cref="FilterOperator.None" />.
/// </summary>
public sealed class FilterableFieldSetTests
{
    [Fact(DisplayName = "All public properties are registered")]
    public void Build_AllPublicProperties_Registered()
    {
        var set = FilterableFieldRegistry.For<TestEntity>();

        var names = set.All.Select(static f => f.Name).ToHashSet(StringComparer.Ordinal);
        names.ShouldContain("Id");
        names.ShouldContain("Name");
        names.ShouldContain("Status");
        names.ShouldContain("CreatedAt");
    }

    [Fact(DisplayName = "[NotMapped] properties are excluded")]
    public void Build_NotMappedProperty_Excluded()
    {
        var set = FilterableFieldRegistry.For<TestEntityWithExclusion>();

        var names = set.All.Select(static f => f.Name).ToHashSet(StringComparer.Ordinal);
        names.ShouldNotContain("PasswordHash");
        names.ShouldNotContain("InternalNotes");
    }

    [Fact(DisplayName = "Property with unsupported type is excluded")]
    public void Build_UnsupportedTypeProperty_Excluded()
    {
        // TestEntityWithByteArray has a byte[] property — no operator set
        // matches it, so it should be excluded from the field set.
        var set = FilterableFieldRegistry.For<TestEntityWithByteArray>();

        var names = set.All.Select(static f => f.Name).ToHashSet(StringComparer.Ordinal);
        names.ShouldNotContain("Blob");
    }

    [Fact(DisplayName = "Find by name is case-insensitive")]
    public void Find_CaseInsensitive()
    {
        var set = FilterableFieldRegistry.For<TestEntity>();

        set.Find("name").ShouldNotBeNull();
        set.Find("NAME").ShouldNotBeNull();
        set.Find("Name").ShouldNotBeNull();
    }

    [Fact(DisplayName = "Find unknown field returns null")]
    public void Find_UnknownField_ReturnsNull()
    {
        var set = FilterableFieldRegistry.For<TestEntity>();

        set.Find("NonExistent").ShouldBeNull();
    }

    [Fact(DisplayName = "String fields get Contains + IContains + In operators")]
    public void Build_StringField_OperatorsIncludeContains()
    {
        var set = FilterableFieldRegistry.For<TestEntity>();
        var nameField = set.Find("Name");

        nameField.ShouldNotBeNull();
        var ops = nameField!.Operators;
        (ops & FilterOperator.Contains).ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.IContains).ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.In).ShouldNotBe(FilterOperator.None);
    }

    [Fact(DisplayName = "Int fields get range operators but not Contains")]
    public void Build_IntField_OperatorsIncludeGtButNotContains()
    {
        var set = FilterableFieldRegistry.For<TestEntity>();
        var idField = set.Find("Id");

        idField.ShouldNotBeNull();
        var ops = idField!.Operators;
        (ops & FilterOperator.Gt).ShouldNotBe(FilterOperator.None);
        (ops & FilterOperator.Contains).ShouldBe(FilterOperator.None);
    }

    [Fact(DisplayName = "For<T>() cache returns same instance on second call")]
    public void For_CachedOnSecondCall()
    {
        var first = FilterableFieldRegistry.For<TestEntity>();
        var second = FilterableFieldRegistry.For<TestEntity>();

        first.ShouldBeSameAs(second);
    }
}

public sealed class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class TestEntityWithExclusion
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [NotMapped]
    public string PasswordHash { get; set; } = string.Empty;

    [NotMapped]
    public string InternalNotes { get; set; } = string.Empty;
}

public sealed class TestEntityWithByteArray
{
    public int Id { get; set; }
    public byte[] Blob { get; set; } = [];
}
