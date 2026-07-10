// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InMemoryWorkloadRegistry — IWorkloadRegistry implementation for v0.1.
// Single dictionary keyed by WorkloadKind; providers register at
// startup. The agent's command dispatcher doesn't use this directly —
// it's used by executors that need to look up a provider by kind.
// ============================================================================

using System.Collections.Concurrent;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Composition;

/// <summary>
/// In-memory registry of <see cref="IWorkloadProvider"/> instances,
/// keyed by the <see cref="WorkloadKind"/> they handle. v0.1 keeps
/// state in-process; restart loses every workload's runtime id
/// (the provider's local handles die with the process anyway).
/// </summary>
internal sealed class InMemoryWorkloadRegistry : IWorkloadRegistry
{
    private readonly ConcurrentDictionary<WorkloadKind, IWorkloadProvider> providers = new();

    /// <inheritdoc />
    public IReadOnlyCollection<WorkloadKind> SupportedKinds
        => providers.Keys.ToArray();

    /// <inheritdoc />
    public IWorkloadProvider? GetProvider(WorkloadKind kind)
    {
        return providers.TryGetValue(kind, out var provider) ? provider : null;
    }

    /// <inheritdoc />
    public void Register(IWorkloadProvider provider)
    {
        if (!providers.TryAdd(provider.Kind, provider))
        {
            var existing = providers[provider.Kind].GetType().Name;
            throw new InvalidOperationException(
                $"InMemoryWorkloadRegistry: provider for kind '{provider.Kind}' " +
                $"already registered (existing: {existing}, new: {provider.GetType().Name}).");
        }
    }
}