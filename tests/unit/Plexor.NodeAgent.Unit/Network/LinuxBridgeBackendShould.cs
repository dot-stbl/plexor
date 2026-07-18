// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LinuxBridgeBackend unit tests — exercise the surface contract
// (cross-backend handle rejection, idempotent detach) without
// shelling out to virsh. The actual virsh invocation is covered
// by integration tests against a real libvirtd.
// ==========================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Plexor.NodeAgent.Providers.Network;
using Plexor.Shared.Compute;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Network;

public sealed class LinuxBridgeBackendShould
{
    [Fact(DisplayName = "Given a handle from another backend, when DetachAsync, then throws ArgumentException")]
    public async Task DetachRejectsForeignHandleAsync()
    {
        var sut = NewBackend();
        var foreignHandle = new NetworkInterfaceHandle(
            BackendName: "ovs-bridge",
            Reference: "br-prod-vpc");

        await Should.ThrowAsync<ArgumentException>(
            () => sut.DetachAsync(foreignHandle, CancellationToken.None));
    }

    [Fact(DisplayName = "Given a missing network, when DetachAsync, then no-op (idempotent)")]
    public async Task DetachIsIdempotentAsync()
    {
        var sut = NewBackend();

        var handle = new NetworkInterfaceHandle(LinuxBridgeBackend.BackendName, "missing-network");
        await sut.DetachAsync(handle, CancellationToken.None);
    }

    [Fact(DisplayName = "Given the canonical BackendName, then it equals 'linux-bridge'")]
    public void BackendNameIsStable()
    {
        LinuxBridgeBackend.BackendName.ShouldBe("linux-bridge");
    }

    /// <summary>
    ///     The AttachAsync → virsh net-define round-trip needs a
    ///     libvirtd on the host. Skipped in unit context; the
    ///     integration suite covers it.
    /// </summary>
    [Fact(DisplayName = "Given a network name, when AttachAsync, then handle.Reference equals the name",
          Skip = "Requires virsh on PATH — covered by integration test suite.")]
    public async Task AttachHandleReferenceIsTheNetworkNameAsync()
    {
        var sut = NewBackend();
        var spec = new NetworkSpec("prod-vpc", NetworkKind.LinuxBridge);

        var handle = await sut.AttachAsync(spec, CancellationToken.None);
        handle.BackendName.ShouldBe(LinuxBridgeBackend.BackendName);
        handle.Reference.ShouldBe("prod-vpc");

        await sut.DetachAsync(handle, CancellationToken.None);
    }

    private static LinuxBridgeBackend NewBackend()
    {
        return new LinuxBridgeBackend(NullLogger<LinuxBridgeBackend>.Instance);
    }
}
