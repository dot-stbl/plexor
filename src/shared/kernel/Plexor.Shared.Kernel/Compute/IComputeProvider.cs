// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IComputeProvider — host-side abstraction over the compute backend that
// owns a workload's runtime lifecycle. The control plane (Plexor.Host)
// talks to the provider; the provider talks to whichever runtime owns
// the actual VM/container/pod lifecycle.
//
// Distinction vs Plexor.Shared.Compute (agent-side). The agent-side
// abstractions (IVolumeBackend, INetworkBackend, IImageRegistry,
// IWorkloadProvider) describe WHAT a single node can do; IComputeProvider
// describes WHAT the host asks the provider to do for a workload row.
// Real implementations forward to one or more node agents; the NoOp
// implementation just acks every call (v1 default until a real
// provider lands).
//
// Lives in Plexor.Shared.Kernel (alongside IQuotaEnforcer,
// IAuditEmitter, etc.) so any module that needs to spawn or manage
// workloads can depend on the seam without crossing module boundaries.
// ============================================================================

namespace Plexor.Shared.Kernel.Compute;

/// <summary>
///     Host-side seam over the compute backend. Implementations are
///     responsible for translating the host's intent (provision /
///     start / stop / delete) into whatever the underlying runtime
///     actually speaks (libvirt, k3s, docker-compose, a future
///     cloud-control-plane adapter).
/// </summary>
/// <remarks>
///     <para><b>Idempotency contract.</b>
///     <see cref="StartVmAsync" />, <see cref="StopVmAsync" />, and
///     <see cref="DeleteVmAsync" /> are idempotent: a second call
///     against an already-running / already-stopped / already-deleted
///     VM is a successful no-op. <see cref="CreateVmAsync" /> is NOT
///     idempotent — a second call with the same operator-facing name
///     raises <see cref="ComputeProviderException" />; the provider
///     is responsible for translating the duplicate-name error into
///     a stable exception type.</para>
///     <para><b>Why the seam exists.</b>
///     Without this interface the workload command handlers would
///     need to know which provider backs the cluster (libvirt on a
///     bare-metal box, k3s on a hybrid cluster, etc.). The seam
///     defers that choice to composition time (DI registration)
///     and keeps the handler logic provider-agnostic.</para>
/// </remarks>
public interface IComputeProvider
{
    /// <summary>
    ///     Stable name of the provider implementation
    ///     (<c>"noop"</c>, <c>"libvirt"</c>, <c>"k3s"</c>, …). Used
    ///     for diagnostics + audit; not a routing key.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Provision a new VM. Returns the provider-assigned
    ///     identifier (libvirt domain UUID, k3s pod UID, …) which
    ///     the host persists on the workload row and passes back on
    ///     every subsequent lifecycle call.
    /// </summary>
    /// <param name="request">VM spec (name, cpu, memory, disk, network).</param>
    /// <param name="cancellationToken">Forwarded to the provider.</param>
    /// <returns>Provider-assigned VM id.</returns>
    /// <exception cref="ComputeProviderException">
    ///     Thrown on provider failure (duplicate name, resource
    ///     exhaustion, backend timeout). The handler maps this to a
    ///     <c>Failed</c> lifecycle state + a domain exception
    ///     surfaced to the operator.
    /// </exception>
    public Task<string> CreateVmAsync(CreateVmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Power on the VM. Idempotent — a second call against an
    ///     already-running VM is a successful no-op. The provider
    ///     is free to optimistically transition the VM to
    ///     <see cref="VmPowerState.Running" /> or to query the
    ///     underlying runtime first; the host doesn't differentiate.
    /// </summary>
    /// <param name="providerVmId">The id returned by <see cref="CreateVmAsync" />.</param>
    /// <param name="cancellationToken">Forwarded to the provider.</param>
    public Task StartVmAsync(string providerVmId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Power off (graceful) the VM. Idempotent — a second call
    ///     against an already-stopped VM is a successful no-op.
    /// </summary>
    /// <param name="providerVmId">The id returned by <see cref="CreateVmAsync" />.</param>
    /// <param name="cancellationToken">Forwarded to the provider.</param>
    public Task StopVmAsync(string providerVmId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Tear down + remove resources associated with the VM.
    ///     Idempotent — a second call against an already-deleted VM
    ///     is a successful no-op.
    /// </summary>
    /// <param name="providerVmId">The id returned by <see cref="CreateVmAsync" />.</param>
    /// <param name="cancellationToken">Forwarded to the provider.</param>
    public Task DeleteVmAsync(string providerVmId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Current power state of the VM, used by the future
    ///     reconciliation job (the v1 hot path relies on the
    ///     optimistic state set by <see cref="StartVmAsync" /> /
    ///     <see cref="StopVmAsync" />, not on this query). Reports
    ///     <see cref="VmPowerState.Unknown" /> when the provider
    ///     can't determine the state (lost backend connection,
    ///     expired handle, etc.).
    /// </summary>
    /// <param name="providerVmId">The id returned by <see cref="CreateVmAsync" />.</param>
    /// <param name="cancellationToken">Forwarded to the provider.</param>
    public Task<VmPowerState> GetPowerStateAsync(string providerVmId, CancellationToken cancellationToken = default);
}

/// <summary>
///     VM lifecycle spec passed to <see cref="IComputeProvider.CreateVmAsync" />.
///     All fields are required; the provider validates ranges against
///     its own quotas / capability report.
/// </summary>
/// <param name="Name">Operator-facing name (unique per cluster). Becomes the basis for the provider's runtime handle (libvirt domain name, k3s pod name, …).</param>
/// <param name="Vcpu">Number of virtual CPUs.</param>
/// <param name="MemoryBytes">RAM in bytes.</param>
/// <param name="DiskPaths">Host-side paths to disk images / volumes to attach as the VM's storage. Empty array for a VM with no disks (provider-specific minimum).</param>
/// <param name="NetworkNames">Logical names of networks to attach the VM's NICs to. Resolution to backend-specific handles (bridge names, OVN logical ports) is the provider's job.</param>
public sealed record CreateVmRequest(
    string Name,
    int Vcpu,
    long MemoryBytes,
    string[] DiskPaths,
    string[] NetworkNames);

/// <summary>
///     Power state reported by <see cref="IComputeProvider.GetPowerStateAsync" />.
///     The set is closed — adding a state is a contract change.
/// </summary>
public enum VmPowerState
{
    /// <summary>VM exists but is powered off.</summary>
    Stopped = 0,

    /// <summary>VM exists and is running.</summary>
    Running = 1,

    /// <summary>VM power state can't be determined (lost backend connection, expired handle).</summary>
    Unknown = 2
}

/// <summary>
///     Thrown by <see cref="IComputeProvider" /> implementations on
///     unrecoverable provider failure (duplicate name, resource
///     exhaustion, backend timeout, network partition). Carries a
///     stable <see cref="Code" /> the host's exception handler
///     branches on (so the operator sees a typed ProblemDetails
///     instead of a raw provider error string).
/// </summary>
/// <remarks>
///     Provider implementations MUST set a non-empty <see cref="Code" />
///     from the canonical set; the host's handler refuses to translate
///     a provider exception with an empty code (it surfaces as 500).
/// </remarks>
public sealed class ComputeProviderException : Exception
{
    /// <summary>
    ///     Stable discriminator code (one of
    ///     <see cref="ComputeProviderErrorCodes" /> constants). The
    ///     Clusters exception handler maps the parent
    ///     <see cref="Exception" /> wrapping this exception to
    ///     ProblemDetails (typed at the boundary that wraps it).
    /// </summary>
    public string Code { get; }

    /// <inheritdoc />
    public ComputeProviderException(string code, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "ComputeProviderException code cannot be null or whitespace.",
                nameof(code));
        }

        Code = code;
    }

    /// <inheritdoc />
    public ComputeProviderException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
///     Canonical error codes for <see cref="ComputeProviderException" />.
///     Adding a code here is a public-contract change — clients branch
///     on these values via <see cref="ComputeProviderException.Code" />.
/// </summary>
public static class ComputeProviderErrorCodes
{
    /// <summary>Provider is reachable but cannot complete the request (duplicate name, resource exhaustion, invalid spec).</summary>
    public const string RequestInvalid = "compute.provider.request_invalid";

    /// <summary>Provider backend unreachable (network partition, transport timeout).</summary>
    public const string Unreachable = "compute.provider.unreachable";

    /// <summary>Provider backend timed out before responding.</summary>
    public const string Timeout = "compute.provider.timeout";

    /// <summary>Provider does not recognise the requested VM id (expired handle, drift between host and backend).</summary>
    public const string NotFound = "compute.provider.not_found";

    /// <summary>Provider authorisation failed (expired token, revoked credentials).</summary>
    public const string Unauthorized = "compute.provider.unauthorized";
}
