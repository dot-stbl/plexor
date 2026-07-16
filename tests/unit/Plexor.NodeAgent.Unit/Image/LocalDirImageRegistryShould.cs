// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LocalDirImageRegistry unit tests. The registry is a thin
// adapter over IOptions<ImageRegistryOptions> — tests cover
// the lookup / not-found / AvailableImages paths.
// ==========================================================================

using Microsoft.Extensions.Options;
using Plexor.NodeAgent.Providers.Image;
using Plexor.Shared.Compute;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Image;

public sealed class LocalDirImageRegistryShould
{
    [Fact(DisplayName = "Given a registered ref, when EnsureLocalAsync, then resolves under RootDirectory")]
    public async Task ResolveRelativePath()
    {
        var opts = Options.Create(new ImageRegistryOptions(
            RootDirectory: "/var/lib/plexor/images",
            Catalog: new Dictionary<string, string>
            {
                ["ubuntu-22.04-cloud"] = "ubuntu-22.04.qcow2",
                ["plexor-fw-1.0"] = "firewall/plexor-fw-1.0.qcow2"
            }));
        var sut = new LocalDirImageRegistry(opts);

        var path = await sut.EnsureLocalAsync("ubuntu-22.04-cloud", CancellationToken.None);

        path.ShouldBe("/var/lib/plexor/images/ubuntu-22.04.qcow2");
    }

    [Fact(DisplayName = "Given an absolute path entry, when EnsureLocalAsync, then returns it unchanged")]
    public async Task ResolveAbsolutePath()
    {
        var opts = Options.Create(new ImageRegistryOptions(
            RootDirectory: "/var/lib/plexor/images",
            Catalog: new Dictionary<string, string>
            {
                ["external"] = "/srv/external/custom.qcow2"
            }));
        var sut = new LocalDirImageRegistry(opts);

        var path = await sut.EnsureLocalAsync("external", CancellationToken.None);

        path.ShouldBe("/srv/external/custom.qcow2");
    }

    [Fact(DisplayName = "Given an unknown ref, when EnsureLocalAsync, then throws UnknownImageException")]
    public async Task RejectUnknownRef()
    {
        var opts = Options.Create(new ImageRegistryOptions(
            RootDirectory: "/var/lib/plexor/images",
            Catalog: new Dictionary<string, string>()));
        var sut = new LocalDirImageRegistry(opts);

        await Should.ThrowAsync<UnknownImageException>(
            () => sut.EnsureLocalAsync("does-not-exist", CancellationToken.None));
    }

    [Fact(DisplayName = "Given an empty catalog, when reading AvailableImages, then returns empty set")]
    public void EmptyCatalogReportsNoImages()
    {
        var opts = Options.Create(ImageRegistryOptions.Empty);
        var sut = new LocalDirImageRegistry(opts);

        sut.AvailableImages.ShouldBeEmpty();
    }
}
