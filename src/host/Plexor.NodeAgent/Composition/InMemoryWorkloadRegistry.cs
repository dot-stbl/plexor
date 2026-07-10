// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InMemoryWorkloadRegistry — IWorkloadRegistry implementation for v0.1.
// DI injects every IWorkloadProvider the agent has registered, and
// we register each one against its Kind. The agent's command
// dispatcher doesn't use this directly — it's used by executors
// that need to look up a provider by kind.
// ============================================================================

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
    private readonly Dictionary<WorkloadKind, IWorkloadProvider> providers = new();

    /// <summary>Build a registry from every <see cref="IWorkloadProvider"/>
    /// DI knows about. Each provider registers itself for the
    /// <see cref="WorkloadKind"/> it handles. Duplicate kinds
    /// throw at startup — pick one technology per Kind.</summary>
    public InMemoryWorkloadRegistry(IEnumerable<IWorkloadProvider> providers)
    {
        foreach (var provider in providers)
        {
            Register(provider);
        }
    }

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
        if (providers.TryGetValue(provider.Kind, out var existing))
        {
            throw new InvalidOperationException(
                $"InMemoryWorkloadRegistry: provider for kind '{provider.Kind}' " +
                $"already registered (existing: {existing.GetType().Name}, " +
                $"new: {provider.GetType().Name}).");
        }

        providers[provider.Kind] = provider;
    }
}