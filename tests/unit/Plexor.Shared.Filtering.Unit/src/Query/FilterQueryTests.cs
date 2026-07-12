// Placeholder for FilterQuery envelope tests.
// TODO(Phase 5.x): test FilterQuery.Normalized() and Skip() math — the
// page-bound clamp logic + 1-based vs 0-based skip calculation. Tests
// live here once the list endpoints (Sigil Phase 4) start using
// FilterQuery.

using Plexor.Shared.Filtering;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Query;

public sealed class FilterQueryTests
{
    [Fact(DisplayName = "FilterQuery defaults page=1 pageSize=25")]
    public void FilterQuery_DefaultValues_AreSensible()
    {
        var query = new FilterQuery();

        query.Page.ShouldBe(1);
        query.PageSize.ShouldBe(25);
        query.Filter.ShouldBeNull();
        query.Sort.ShouldBeNull();
    }

    [Fact(DisplayName = "Skip math: page=1 pageSize=25 -> 0")]
    public void FilterQuery_Skip_PageOne_IsZero()
    {
        var query = new FilterQuery { Page = 1, PageSize = 25 };

        query.Skip().ShouldBe(0);
    }

    [Fact(DisplayName = "Skip math: page=4 pageSize=25 -> 75")]
    public void FilterQuery_Skip_PageFour_IsSeventyFive()
    {
        var query = new FilterQuery { Page = 4, PageSize = 25 };

        query.Skip().ShouldBe(75);
    }
}
