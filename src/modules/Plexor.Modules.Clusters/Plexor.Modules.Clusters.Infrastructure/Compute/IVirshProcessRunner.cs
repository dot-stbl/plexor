// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IVirshProcessRunner — test seam for the libvirt provider's
// Process.Start call. The real impl shells out to `virsh -c <uri>
// <subcommand> <args...>` and reads stdout/stderr; the seam lets
// unit tests substitute the runner and assert the exact args
// without standing up a Process.
//
// Why this exists. The IComputeProvider tests need to verify that
// CreateVmAsync translates a CreateVmRequest into the right
// virt-install flags. Mocking the `Process` class directly is
// brittle (sealed type, dozens of APIs, threading concerns); an
// interface seam is the path the codebase uses everywhere else
// (see IAuditEmitter, IBrandingService, …).
// ============================================================================

using Plexor.Shared.Kernel.Compute;

namespace Plexor.Modules.Clusters.Infrastructure.Compute;

/// <summary>
///     Run a single <c>virsh</c> invocation and return stdout.
///     Implementations translate non-zero exits into a typed
///     <see cref="ComputeProviderException" /> with a stable code.
/// </summary>
/// <remarks>
///     Implementations are stateless / thread-safe — every call
///     constructs a fresh process; the runner captures no
///     per-call mutable state. Registered as a singleton in
///     <c>ClustersInfrastructureInstaller</c>.
/// </remarks>
public interface IVirshProcessRunner
{
    /// <summary>
    ///     Run <c>virsh -c &lt;ConnectionUri&gt; &lt;subcommand&gt;
    ///     &lt;args...&gt;</c> and return trimmed stdout.
    /// </summary>
    /// <param name="subcommand">
    ///     The virsh subcommand (e.g. <c>start</c>, <c>shutdown</c>,
    ///     <c>undefine</c>, <c>domstate</c>, or <c>virt-install</c>).
    /// </param>
    /// <param name="args">
    ///     Additional arguments appended after the subcommand.
    ///     Passed verbatim via <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList" />,
    ///     so embedded spaces and quotes are preserved (no shell).
    /// </param>
    /// <param name="cancellationToken">
    ///     Forwarded to <see cref="System.Diagnostics.Process.WaitForExitAsync(System.Threading.CancellationToken)" />.
    /// </param>
    /// <returns>The trimmed stdout of the virsh invocation.</returns>
    /// <exception cref="ComputeProviderException">
    ///     Thrown when the process fails to start, exits non-zero,
    ///     or the call exceeds the configured timeout. The
    ///     exception's <see cref="ComputeProviderException.Code" />
    ///     is one of the canonical
    ///     <see cref="ComputeProviderErrorCodes" /> values.
    /// </exception>
    public Task<string> RunAsync(
        string subcommand,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken);
}
