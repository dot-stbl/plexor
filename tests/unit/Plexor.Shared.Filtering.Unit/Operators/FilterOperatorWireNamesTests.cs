// Tests for FilterOperatorWireNames — the kebab-string <-> C# flag converter.
// The wire names map onto the kubb plugin's FilterOperatorName union
// (frontend client reads them from the OpenAPI document).
//
// TODO(Phase 5.x): add tests for all 16 operators once the API surface
// is locked. Currently this file is a single smoke test to confirm
// the wire-name conventions stay stable across renames.

using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Operators;

/// <summary>
///     Smoke tests for <see cref="FilterOperatorWireNames" />. The full
///     table — one [Theory] row per operator — lands in Phase 5.x once
///     the operator enum is locked.
/// </summary>
public sealed class FilterOperatorWireNamesTests
{
    [Fact(DisplayName = "NamesFor on empty operator set returns empty list")]
    public void NamesFor_Empty_ReturnsEmpty()
    {
        var names = FilterOperator.None.NamesFor();

        names.ShouldBeEmpty();
    }

    [Fact(DisplayName = "NamesFor with Eq returns single 'eq'")]
    public void NamesFor_Eq_ReturnsEq()
    {
        var names = FilterOperator.Eq.NamesFor();

        names.ShouldHaveSingleItem();
        names[0].ShouldBe("eq");
    }

    [Fact(DisplayName = "NamesFor with Eq|In returns both names")]
    public void NamesFor_FlagsBitmask_ExpandsBoth()
    {
        var combined = FilterOperator.Eq | FilterOperator.In;
        var names = combined.NamesFor();

        names.ShouldContain("eq");
        names.ShouldContain("in");
    }
}
