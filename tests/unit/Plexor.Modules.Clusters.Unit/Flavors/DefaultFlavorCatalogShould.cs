// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DefaultFlavorCatalogShould — verify the v0.1 flavor catalog
// contents and lookup behavior. Pure-function tests; no DI, no I/O.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Flavors;
using Plexor.Shared.NodeApi;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Flavors;

public sealed class DefaultFlavorCatalogShould
{
    [Fact(DisplayName = "Given the default catalog, when List, then returns three flavors: small / medium / large")]
    public void ListReturnsThreeSeededFlavors()
    {
        var sut = new DefaultFlavorCatalog();

        var flavors = sut.List();

        flavors.Count.ShouldBe(3);
        flavors.Select(flavor => flavor.Name)
               .ShouldBe(["small", "medium", "large"], ignoreOrder: false);
    }

    [Theory(DisplayName = "Given a known flavor name, when Get, then returns the matching flavor")]
    [InlineData("small", 1, 2, 20)]
    [InlineData("medium", 2, 4, 40)]
    [InlineData("large", 4, 8, 80)]
    public void GetReturnsExpectedFlavor(
        string name,
        int vcpu,
        int ramGb,
        int diskGb)
    {
        var sut = new DefaultFlavorCatalog();

        var flavor = sut.Get(name);

        flavor.ShouldNotBeNull();
        flavor.Name.ShouldBe(name);
        flavor.Default.Vcpu.ShouldBe(vcpu);
        flavor.Default.RamBytes.ShouldBe((long)ramGb * 1024 * 1024 * 1024);
        flavor.Default.DiskBytes.ShouldBe((long)diskGb * 1024 * 1024 * 1024);
        flavor.Default.ImageRef.ShouldBe("ubuntu-22.04-cloud");
        flavor.Default.NetworkName.ShouldBeNull();
    }

    [Theory(DisplayName = "Given an unknown flavor name, when Get, then returns null")]
    [InlineData("xlarge")]
    [InlineData("")]
    [InlineData("SMALL")] // case-sensitive on purpose
    public void GetReturnsNullForUnknown(string name)
    {
        var sut = new DefaultFlavorCatalog();

        sut.Get(name).ShouldBeNull();
    }

    [Fact(DisplayName = "Given every flavor, when List, then the Default.ImageRef is the bundled ubuntu-22.04-cloud")]
    public void EveryFlavorUsesBundledUbuntuImage()
    {
        var sut = new DefaultFlavorCatalog();

        var flavors = sut.List();

        flavors.ShouldAllBe(flavor => flavor.Default.ImageRef == "ubuntu-22.04-cloud");
    }

    [Fact(DisplayName = "Given two calls to List, when compared, then return the same singleton array")]
    public void ListReturnsTheSameArray()
    {
        var sut = new DefaultFlavorCatalog();

        var first = sut.List();
        var second = sut.List();

        ReferenceEquals(first, second).ShouldBeTrue();
    }
}