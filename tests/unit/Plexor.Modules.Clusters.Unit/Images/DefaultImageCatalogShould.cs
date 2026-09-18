// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DefaultImageCatalogShould — verify the v0.1 image catalog
// contents, lookup, and tag filtering. Pure-function tests; no
// DI, no I/O.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Images;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Images;

public sealed class DefaultImageCatalogShould
{
    [Fact(DisplayName = "Given the default catalog, when List with no filter, then returns the seeded ubuntu-22.04-cloud image")]
    public void ListReturnsSeededImage()
    {
        var sut = new DefaultImageCatalog();

        var images = sut.List();

        images.Count.ShouldBe(1);
        images[0].Name.ShouldBe("ubuntu-22.04-cloud");
        images[0].Source.ShouldBe(
            "https://cloud-images.ubuntu.com/jammy/current/jammy-server-cloudimg-amd64.img");
    }

    [Fact(DisplayName = "Given an exact name, when Get, then returns the matching image")]
    public void GetReturnsExactMatch()
    {
        var sut = new DefaultImageCatalog();

        var image = sut.Get("ubuntu-22.04-cloud");

        image.ShouldNotBeNull();
        image.Name.ShouldBe("ubuntu-22.04-cloud");
        image.Source.ShouldStartWith("https://cloud-images.ubuntu.com/");
        image.Tags.ShouldContain("ubuntu");
        image.Tags.ShouldContain("lts");
        image.Tags.ShouldContain("cloud");
        image.Tags.ShouldContain("amd64");
    }

    [Theory(DisplayName = "Given an unknown image name, when Get, then returns null")]
    [InlineData("debian-12")]
    [InlineData("")]
    [InlineData("Ubuntu-22.04-cloud")] // case-sensitive
    public void GetReturnsNullForUnknown(string name)
    {
        var sut = new DefaultImageCatalog();

        sut.Get(name).ShouldBeNull();
    }

    [Fact(DisplayName = "Given tags that all match the seeded image, when List, then returns the image")]
    public void ListWithMatchingTagsReturnsImage()
    {
        var sut = new DefaultImageCatalog();

        var images = sut.List(["ubuntu", "amd64"]);

        images.Count.ShouldBe(1);
        images[0].Name.ShouldBe("ubuntu-22.04-cloud");
    }

    [Fact(DisplayName = "Given a tag that no image has, when List, then returns empty")]
    public void ListWithUnmatchedTagReturnsEmpty()
    {
        var sut = new DefaultImageCatalog();

        var images = sut.List(["rhel"]);

        images.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Given a mixed tag list (one match + one miss), when List, then returns empty (logical AND)")]
    public void ListWithMixedTagsReturnsEmpty()
    {
        var sut = new DefaultImageCatalog();

        var images = sut.List(["ubuntu", "rhel"]);

        images.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Given an empty tag list, when List, then returns every image (no filter)")]
    public void ListWithEmptyTagFilterReturnsAll()
    {
        var sut = new DefaultImageCatalog();

        var all = sut.List();
        var withEmptyFilter = sut.List([]);

        withEmptyFilter.Count.ShouldBe(all.Count);
    }

    [Fact(DisplayName = "Given two calls to List, when compared, then return the same singleton array")]
    public void ListReturnsTheSameArray()
    {
        var sut = new DefaultImageCatalog();

        var first = sut.List();
        var second = sut.List();

        ReferenceEquals(first, second).ShouldBeTrue();
    }
}
