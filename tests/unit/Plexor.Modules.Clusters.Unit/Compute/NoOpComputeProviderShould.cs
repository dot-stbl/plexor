// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NoOpComputeProvider tests — the v1 default IComputeProvider. Every
// method acks; CreateVmAsync returns a synthetic "noop-{guid}" handle;
// Start/Stop/Delete are idempotent no-ops. Tests run against the
// concrete NoOpComputeProvider (no mocking — the type is a pure stub
// and the behaviour IS the contract).
// ============================================================================

using Plexor.Modules.Clusters.Infrastructure.Compute;
using Plexor.Shared.Kernel.Compute;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Compute;

public sealed class NoOpComputeProviderShould
{
    private readonly NoOpComputeProvider _sut = new();

    [Fact(DisplayName = "Given any spec, when CreateVmAsync, then returns a noop-prefixed handle")]
    public async Task CreateVmAsyncReturnsSyntheticHandleAsync()
    {
        var handle = await _sut.CreateVmAsync(
            new CreateVmRequest(
                Name: "web-1",
                Vcpu: 2,
                MemoryBytes: 1024 * 1024 * 1024,
                DiskPaths: ["/var/lib/plexor/web-1.qcow2"],
                NetworkNames: ["default"]),
            CancellationToken.None);

        handle.ShouldNotBeNullOrEmpty();
        handle.ShouldStartWith("noop-");
    }

    [Fact(DisplayName = "Given any handle, when Start/Stop/Delete, then all complete without error (idempotent)")]
    public async Task LifecycleActionsAreIdempotentAsync()
    {
        await _sut.StartVmAsync("noop-handle-1");
        await _sut.StopVmAsync("noop-handle-1");
        await _sut.DeleteVmAsync("noop-handle-1");

        // No assertions — completing without throwing is the
        // contract. The next call against the same handle would
        // also succeed (a real provider would no-op it).
        await _sut.StartVmAsync("noop-handle-1");
        await _sut.StopVmAsync("noop-handle-1");
        await _sut.DeleteVmAsync("noop-handle-1");
    }

    [Fact(DisplayName = "Given any handle, when GetPowerStateAsync, then reports Running")]
    public async Task GetPowerStateReturnsRunningAsync()
    {
        var state = await _sut.GetPowerStateAsync("noop-handle-1");

        state.ShouldBe(VmPowerState.Running);
    }
}
