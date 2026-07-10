// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LocalWorkload — runtime shape of a workload on the node, as the
// agent sees it. The shared wire contract (Plexor.Shared.NodeApi) only
// carries the workload id; everything else is the agent's local
// bookkeeping. Per-provider details (libvirt UUID, k8s resource name)
// live inside each IWorkloadProvider implementation.
// ============================================================================

namespace Plexor.Shared.Workloads;

/// <summary>
/// Lifecycle state of a workload on the node. Reported to the
/// control plane via the heartbeat (or on demand) so the UI can
/// show "provisioning / running / failed" without a per-provider
/// translator on the Host side.
/// </summary>
public enum WorkloadState
{
    /// <summary>Provider is creating the workload (image download, IP
    /// assignment, etc.). Not yet started.</summary>
    Provisioning,

    /// <summary>Workload is booted and accepting traffic.</summary>
    Running,

    /// <summary>Workload is gracefully shut down but the resources
    /// are still allocated (can be re-started without re-create).</summary>
    Stopped,

    /// <summary>Last lifecycle operation failed. The control plane
    /// should mark the workload <c>Failed</c> in its aggregate view
    /// and surface a repair flow to the user.</summary>
    Failed,

    /// <summary>Provider can't determine the state (lost connection,
    /// unknown domain id, etc.). Reported as <c>Degraded</c> upstream.</summary>
    Unknown,
}

/// <summary>
/// The agent's local view of a workload. The control plane only
/// knows <see cref="Id"/> (and the kind, from the original
/// <c>workload.create</c> command); everything else is local state the
/// provider keeps in its own data structures.
/// </summary>
/// <param name="CreatedAt">When the provider finished provisioning.</param>
/// <param name="StartedAt">When the workload was last started (null
/// while it has never booted).</param>
public sealed record LocalWorkload(
    Guid Id,
    string Name,
    Plexor.Shared.NodeApi.WorkloadKind Kind,
    WorkloadState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt);
