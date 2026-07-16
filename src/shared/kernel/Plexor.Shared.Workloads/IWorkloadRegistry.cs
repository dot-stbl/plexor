// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IWorkloadRegistry — the dispatcher side of the provider abstraction.
// One entry per (provider, kind); the command dispatcher looks the
// right one up by WorkloadKind. The implementation lives in
// Plexor.NodeAgent's composition layer (in-memory singleton for v0.1).
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.Shared.Workloads;

/// <summary>
///     Lookup of <see cref="IWorkloadProvider" /> by
///     <see cref="WorkloadKind" />. The command dispatcher calls
///     <see cref="GetProvider" /> for each incoming command; a null
///     result means the node doesn't support that kind and the command
///     fails with a clear error.
/// </summary>
public interface IWorkloadRegistry
{
    /// <summary>
    ///     The kinds the local node supports. A non-empty
    ///     intersection with the kinds the control plane can issue is
    ///     required for any command to be executable.
    /// </summary>
    public IReadOnlyCollection<WorkloadKind> SupportedKinds { get; }

    /// <summary>
    ///     Look up the provider for <paramref name="kind" />, or
    ///     <c>null</c> if the node doesn't support that kind.
    /// </summary>
    /// <param name="kind"></param>
    public IWorkloadProvider? GetProvider(WorkloadKind kind);

    /// <summary>
    ///     Register a provider at startup. The registry throws
    ///     if a provider for the same <see cref="IWorkloadProvider.Kind" />
    ///     is already registered.
    /// </summary>
    /// <param name="provider"></param>
    public void Register(IWorkloadProvider provider);
}
