// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NoOpComputeProvider — the v1 default IComputeProvider.
//
// Every method acks successfully: CreateVmAsync mints a UUID-shaped
// provider handle ("noop-{guid}") so callers have something to
// persist on the workload row; Start/Stop/Delete are no-op async
// returns. GetPowerStateAsync returns Running so the optimistic
// lifecycle state set by the handlers matches what the provider
// would report back.
//
// Why this exists. Real compute providers (libvirt, k3s, docker-
// compose, ...) live in separate future modules. Until one of those
// modules is registered as the IComputeProvider, the workload
// handlers need a default that satisfies the seam — otherwise the
// host can't even create a workload row. NoOp is that default.
//
// The audit log distinguishes NoOp-created VMs from real ones
// through the IComputeProvider.Name property ("noop" vs "libvirt"
// etc.). The lifecycle event trail carries the provider handle so
// operators can tell at a glance which workloads are real.
// ============================================================================

using Plexor.Shared.Kernel.Compute;

namespace Plexor.Modules.Clusters.Infrastructure.Compute;

/// <summary>
///     Stub <see cref="IComputeProvider" /> used until a real
///     provider (libvirt, k3s, docker-compose, …) is registered.
///     Every call acks successfully; <see cref="CreateVmAsync" />
///     returns a synthetic <c>"noop-{guid}"</c> handle so the
///     workload row has a non-null <c>provider_vm_id</c> for
///     debugging.
/// </summary>
/// <remarks>
///     Thread-safe — every method is a pure async return; the
///     only state is the synthetic <c>noop-{guid}</c> handle minted
///     per <see cref="CreateVmAsync" /> call. Registered as a
///     singleton in
///     <see cref="Installers.ClustersInfrastructureInstaller" />.
/// </remarks>
public sealed class NoOpComputeProvider : IComputeProvider
{
    /// <inheritdoc />
    public string Name => "noop";

    /// <inheritdoc />
    public Task<string> CreateVmAsync(
        CreateVmRequest request,
        CancellationToken cancellationToken = default)
    {
        // Synthetic handle — recognisably a NoOp so operators can
        // tell at a glance which workloads are running against the
        // stub vs a real provider. Collisions are vanishingly
        // unlikely (UUIDv7 has a 122-bit random tail) but a
        // duplicate suffix would surface as an audit-trail anomaly,
        // not as a data-integrity issue.
        var handle = $"noop-{Guid.NewGuid()}";
        return Task.FromResult(handle);
    }

    /// <inheritdoc />
    public Task StartVmAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopVmAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteVmAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<VmPowerState> GetPowerStateAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(VmPowerState.Running);
    }
}
