// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadIdMap — local id ↔ domain-name tracking for the agent.
//
// Each provider's CreateAsync assigns a fresh Guid (the "local
// id") and uses the WorkloadSpec.Name as the libvirt domain name
// (which must be unique and DNS-safe). The agent's control loop
// references workloads by Guid; the provider translates to/from
// the libvirt domain name via this map.
//
// v0.1: in-memory ConcurrentDictionary. State is lost on agent
// restart (along with every workload's local id) — providers
// that survive restart must re-list their workloads and rebuild
// the map at boot.
// ============================================================================

using System.Collections.Concurrent;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers.Common;

/// <summary>
/// Per-provider map of <c>localId</c> to a snapshot of the
/// workload's runtime state. Snapshot includes the libvirt
/// domain name (so start/stop/delete can build the right
/// virsh command) and the current
/// <see cref="WorkloadState"/>.
/// </summary>
public sealed class WorkloadIdMap
{
    private readonly ConcurrentDictionary<Guid, WorkloadIdMapEntry> entries = new();

    /// <summary>Register a new workload. Throws if the local id
    /// is already present (idempotent creates are the caller's
    /// responsibility).</summary>
    public void Register(Guid localId, string domainName, WorkloadKind kind)
    {
        var entry = new WorkloadIdMapEntry(domainName, kind, WorkloadState.Provisioning);
        if (!entries.TryAdd(localId, entry))
        {
            throw new InvalidOperationException(
                $"WorkloadIdMap: id {localId} already registered.");
        }
    }

    /// <summary>Return the entry for the given id, or throw
    /// if the id is unknown. The provider catches and reports
    /// Failed back to the host.</summary>
    public WorkloadIdMapEntry GetOrThrow(Guid localId)
    {
        if (!entries.TryGetValue(localId, out var entry))
        {
            throw new InvalidOperationException(
                $"WorkloadIdMap: no entry for id {localId}.");
        }
        return entry;
    }

    /// <summary>Update the state snapshot for the given id.
    /// Does not change the domain name or kind.</summary>
    public void SetState(Guid localId, WorkloadState state)
    {
        var current = GetOrThrow(localId);
        entries[localId] = current with { State = state };
    }

    /// <summary>Remove the entry. Returns true if the id was
    /// known. Idempotent: removing a missing id is a no-op
    /// success (matches the provider's delete semantics).</summary>
    public bool Remove(Guid localId)
    {
        return entries.TryRemove(localId, out _);
    }

    /// <summary>Snapshot of the map for a list call. The
    /// provider's ListAsync builds the return value from this.</summary>
    public IReadOnlyList<LocalWorkload> Snapshot(string agentHostname)
    {
        var now = DateTimeOffset.UtcNow;
        return entries
            .Select(kvp => new LocalWorkload(
                Id: kvp.Key,
                Name: kvp.Value.DomainName,
                Kind: kvp.Value.Kind,
                State: kvp.Value.State,
                CreatedAt: now,
                StartedAt: kvp.Value.State == WorkloadState.Running ? now : null))
            .ToArray();
    }
}

/// <summary>One entry in <see cref="WorkloadIdMap"/>: the
/// libvirt domain name + the workload's current runtime state.</summary>
public sealed record WorkloadIdMapEntry(
    string DomainName,
    WorkloadKind Kind,
    WorkloadState State);