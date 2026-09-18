// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtKvmProvider unit tests — exercise the surface contract of the
// provider that doesn't require shelling out to virsh + qemu-img:
//
//   - VolumeSpec.SizeBytes honours explicit DiskBytes; falls back to
//     RAM * 4 when DiskBytes is null (the "control plane forgot to
//     send disk size" path).
//   - VolumeSpec.BaseImageRef is forwarded from the provider config.
//   - NetworkSpec.Name is forwarded from the provider config.
//
// The integration test (P1 #12 — boot proof) covers the actual
// virsh define / start / undefine cycle on a host with KVM installed.
// ============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.NodeAgent.Providers;
using Plexor.NodeAgent.Providers.Network;
using Plexor.NodeAgent.Providers.Storage;
using Plexor.Shared.Compute;
using Plexor.Shared.NodeApi;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Providers;

public sealed class LibvirtKvmProviderShould
{
    [Fact(DisplayName = "Given a WorkloadSpec with DiskBytes=50 GiB, when CreateAsync, then volume SizeBytes is 50 GiB (no RAM fallback)")]
    public async Task DiskBytesOverridesRamFallbackAsync()
    {
        var (sut, _, _, _) = NewProvider(out var volumeCalls);
        var spec = NewSpec("vm-disk-explicit", /*lang=json,strict*/ """
            {
              "Vcpu": 2,
              "RamBytes": 4294967296,
              "DiskBytes": 53687091200,
              "NetworkName": "default"
            }
            """);

        try
        {
            await sut.CreateAsync(spec, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Expected on Windows / hosts without virsh; we only
            // care about the volume-spec assertion above.
        }

        volumeCalls.ShouldHaveSingleItem();
        var captured = volumeCalls[0];
        captured.SizeBytes.ShouldBe(53687091200L);
    }

    [Fact(DisplayName = "Given a WorkloadSpec with DiskBytes=null, when CreateAsync, then volume SizeBytes falls back to RAM * 4")]
    public async Task NullDiskBytesFallsBackToRamTimesFourAsync()
    {
        var (sut, _, _, _) = NewProvider(out var volumeCalls);
        var spec = NewSpec("vm-disk-default", /*lang=json,strict*/ """
            {
              "Vcpu": 2,
              "RamBytes": 2147483648,
              "NetworkName": "default"
            }
            """);

        try
        {
            await sut.CreateAsync(spec, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { /* see above */ }

        volumeCalls.ShouldHaveSingleItem();
        var captured = volumeCalls[0];
        captured.SizeBytes.ShouldBe(2147483648L * 4L);
    }

    [Fact(DisplayName = "Given a WorkloadSpec with BaseImageRef, when CreateAsync, then volume carries the ref")]
    public async Task BaseImageRefForwardedToVolumeAsync()
    {
        var (sut, _, _, _) = NewProvider(out var volumeCalls);
        var spec = NewSpec("vm-image", /*lang=json,strict*/ """{"Vcpu":2,"RamBytes":2147483648,"BaseImageRef":"ubuntu-22.04-cloud"}""");

        try
        {
            await sut.CreateAsync(spec, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { /* see above */ }

        volumeCalls.ShouldHaveSingleItem();
        volumeCalls[0].BaseImageRef.ShouldBe("ubuntu-22.04-cloud");
    }

    [Fact(DisplayName = "Given a WorkloadSpec with NetworkName, when CreateAsync, then network backend receives the name")]
    public async Task NetworkNameForwardedToNetworkBackendAsync()
    {
        var (sut, _, networks, _) = NewProvider(out _);
        var spec = NewSpec("vm-net", /*lang=json,strict*/ """{"Vcpu":2,"RamBytes":2147483648,"NetworkName":"br-tenant-a"}""");

        try
        {
            await sut.CreateAsync(spec, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { /* see above */ }

        await networks.Received(1).AttachAsync(
            Arg.Is<NetworkSpec>(static s => s.Name == "br-tenant-a"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given any provider, when Kind, then returns WorkloadKind.Vm")]
    public void KindReturnsVm()
    {
        var (sut, _, _, _) = NewProvider(out _);
        sut.Kind.ShouldBeOfType<WorkloadKind.Vm>();
    }

    [Fact(DisplayName = "Given a new provider, when ListAsync with no created workloads, then returns empty")]
    public async Task ListAsyncReturnsEmptyInitiallyAsync()
    {
        var (sut, _, _, _) = NewProvider(out _);
        var list = await sut.ListAsync(CancellationToken.None);
        list.ShouldBeEmpty();
    }

    private static (LibvirtKvmProvider Sut, IVolumeBackend Volumes, INetworkBackend Networks, TimeProvider Clock) NewProvider(
        out List<VolumeSpec> volumeCalls)
    {
        var volumeStore = new List<VolumeSpec>();
        var volumes = Substitute.For<IVolumeBackend>();
        volumes.CreateAsync(Arg.Do<VolumeSpec>(volumeStore.Add), Arg.Any<CancellationToken>())
            .Returns(new VolumeHandle(LocalDirStorage.BackendName, "/var/lib/plexor/volumes/test.qcow2"));

        var networks = Substitute.For<INetworkBackend>();
        networks.AttachAsync(Arg.Any<NetworkSpec>(), Arg.Any<CancellationToken>())
            .Returns(new NetworkInterfaceHandle(LinuxBridgeBackend.BackendName, "br-default"));

        volumeCalls = volumeStore;

        // The provider's CreateAsync ends up calling LibvirtRunner
        // (file-static), which shells out to virsh. On a host
        // without virsh this throws InvalidOperationException
        // (the LibvirtRunner contract). We can't unit-test that
        // path without refactoring LibvirtRunner to an injectable
        // interface — that's Tier 5 work. Until then the test
        // catches the InvalidOperationException as a
        // "libvirt not available" marker and still asserts the
        // pre-virsh side-effects (volume + network wiring).
        var clock = TimeProvider.System;
        var provider = new LibvirtKvmProvider(
            volumes,
            networks,
            NullLogger<LibvirtKvmProvider>.Instance,
            clock);

        return (provider, volumes, networks, clock);
    }

    private static WorkloadSpec NewSpec(string name, string configJson)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(configJson);
        return new WorkloadSpec(new WorkloadKind.Vm(), name, element);
    }
}

