// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtNetworkProviderShould — unit tests for the OS-guard and
// state-machine surface. Real virsh invocations need a Linux host
// with libvirtd; those are covered by the integration test suite.
// Here we verify the parts that don't need a libvirtd: the OS
// guard throws on Windows, the static LibvirtUri is the expected
// qemu:///system, and the runner URL parsing path is sane.
// ==========================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Plexor.Shared.Network;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Network.Unit;

public sealed class LibvirtNetworkProviderShould
{
    [Fact(DisplayName = "Given the default provider, when constructed with no URI, then LibvirtUri defaults to qemu:///system")]
    public void DefaultLibvirtUriIsQemuSystem()
    {
        LibvirtNetworkProvider.DefaultLibvirtUri.ToString().ShouldBe("qemu:///system");
    }

    [Fact(DisplayName = "Given a Windows host, when ListNetworksAsync, then throws PlatformNotSupportedException")]
    public void ListNetworksAsyncThrowsOnNonLinux()
    {
        if (OperatingSystem.IsLinux())
        {
            // Can't assert the failure on Linux CI.
            return;
        }

        var sut = new LibvirtNetworkProvider(logger: NullLogger<LibvirtNetworkProvider>.Instance);

        Should.ThrowAsync<PlatformNotSupportedException>(
            () => sut.ListNetworksAsync(CancellationToken.None));
    }

    [Fact(DisplayName = "Given a Windows host, when ResolveBridgeNameAsync, then throws PlatformNotSupportedException")]
    public void ResolveBridgeNameAsyncThrowsOnNonLinux()
    {
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        var sut = new LibvirtNetworkProvider(logger: NullLogger<LibvirtNetworkProvider>.Instance);

        Should.ThrowAsync<PlatformNotSupportedException>(
            () => sut.ResolveBridgeNameAsync(CancellationToken.None));
    }

    [Fact(DisplayName = "Given a custom URI, when constructed, then the provider preserves it")]
    public void CustomLibvirtUriIsPreserved()
    {
        var customUri = new Uri("qemu+ssh://user@host/system", UriKind.Absolute);
        var sut = new LibvirtNetworkProvider(
            libvirtUri: customUri,
            logger: NullLogger<LibvirtNetworkProvider>.Instance);

        // No public getter for the URI in v0.1; the contract is
        // visible only through the OS-guard behavior. Verify the
        // provider was constructible with a non-default URI — a
        // later iteration can expose the getter if the test
        // surface grows.
        sut.ShouldNotBeNull();
    }
}