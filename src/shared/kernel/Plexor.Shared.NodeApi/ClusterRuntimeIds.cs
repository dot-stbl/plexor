// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterRuntimeIds — closed set of runtime identifiers that a
// cluster can declare at create-time. The runtime is immutable for
// the lifetime of the cluster (switching runtime = tear down and
// recreate). Three runtimes ship in v0.1 (per the runtime-providers
// plan Q3 decision): docker-compose, podman-quadlet, k3s.
//
// Why a closed set: each runtime implementation ships its own
// provider binary + manifest renderer + state-polling logic. Adding
// a runtime is an explicit code change (new IWorkloadProvider
// implementation in Plexor.NodeAgent + new mapping here). Open-ended
// runtime strings would let stale cluster rows reference kinds that
// the running agent can't actually execute.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Closed set of cluster-level runtime identifiers. Each constant
///     here must have a matching <see cref="WorkloadKind" /> variant
///     and a corresponding <c>IWorkloadProvider</c> implementation
///     in <c>Plexor.NodeAgent</c>.
/// </summary>
public static class ClusterRuntimeIds
{
    /// <summary>
    ///     <c>docker-compose</c> — single-host multi-container
    ///     workloads via <c>docker compose up -d</c>. Default for
    ///     most dev / single-server deployments.
    /// </summary>
    public const string DockerCompose = "docker-compose";

    /// <summary>
    ///     <c>podman-quadlet</c> — single-host container as a systemd
    ///     quadlet unit (<c>&lt;name&gt;.container</c>). Rootless-friendly
    ///     alternative to docker-compose; default for RHEL /
    ///     Alma / Fedora hosts.
    /// </summary>
    public const string PodmanQuadlet = "podman-quadlet";

    /// <summary>
    ///     <c>k3s</c> — Kubernetes workloads deployed via
    ///     <c>kubectl apply -k</c> against an existing k3s cluster.
    ///     Provisioning of the k3s cluster itself is out of scope
    ///     (see <c>plan-k8s</c>); the runtime impl assumes k3s
    ///     is already installed on every node.
    /// </summary>
    public const string K3s = "k3s";

    /// <summary>
    ///     Default runtime for new clusters when the operator does
    ///     not specify one. <see cref="DockerCompose" /> is the
    ///     lowest-common-denominator (works on any Linux dev box).
    /// </summary>
    public const string Default = DockerCompose;

    /// <summary>
    ///     All supported runtime ids, in declaration order. Used for
    ///     validation against cluster input + to drive the runtime
    ///     selector at the NodeAgent side.
    /// </summary>
    public static readonly IReadOnlyCollection<string> All =
        [DockerCompose, PodmanQuadlet, K3s];

    /// <summary>
    ///     Returns true when <paramref name="runtimeId" /> is one
    ///     of the closed set of supported runtimes. Null or empty
    ///     input returns false (callers should treat that as a
    ///     missing-value error, not as "default").
    /// </summary>
    /// <param name="runtimeId">Candidate runtime identifier.</param>
    public static bool IsValid(string? runtimeId)
    {
        return runtimeId is not null && All.Contains(runtimeId);
    }
}
