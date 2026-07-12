// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IWorkloadProvider — one implementation per WorkloadKind. The agent's
// command dispatcher looks up the right provider by Kind and calls
// these methods. Each implementation owns its runtime state (libvirt
// domain id, k8s resource name, etc.); the shared contract is the
// interface signature only.
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.Shared.Workloads;

/// <summary>
///     A provider that knows how to create / start / stop / delete a
///     specific kind of workload. One implementation per
///     <see cref="WorkloadKind" /> (libvirt/KVM, libvirt/LXC, k3s, podman).
///     The agent's dispatcher looks the right one up by
///     <see cref="Kind" />.
/// </summary>
public interface IWorkloadProvider
{
    /// <summary>
    ///     Workload kind this provider handles. The dispatcher
    ///     matches on this value.
    /// </summary>
    public WorkloadKind Kind { get; }

    /// <summary>
    ///     Provision a new workload. The provider parses
    ///     <see cref="WorkloadSpec.Config" /> per its own schema (the shared
    ///     contract is opaque JSON).
    /// </summary>
    public Task<LocalWorkload> CreateAsync(WorkloadSpec spec, CancellationToken cancellationToken);

    /// <summary>
    ///     Boot a previously provisioned workload. Throws
    ///     <see cref="InvalidOperationException" /> if the workload is
    ///     already running or doesn't exist.
    /// </summary>
    public Task<LocalWorkload> StartAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    ///     Gracefully shut down a running workload. Throws
    ///     <see cref="InvalidOperationException" /> if the workload is
    ///     already stopped or doesn't exist.
    /// </summary>
    public Task<LocalWorkload> StopAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    ///     Remove the workload and all of its backing storage.
    ///     Idempotent: deleting a missing workload is a no-op success.
    /// </summary>
    public Task<LocalWorkload> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    ///     List every workload this provider manages on the local
    ///     node. Used at boot to reconcile the control plane's view with
    ///     what's actually running.
    /// </summary>
    public Task<IReadOnlyList<LocalWorkload>> ListAsync(CancellationToken cancellationToken);
}
