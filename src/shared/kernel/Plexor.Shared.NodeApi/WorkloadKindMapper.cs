// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadKindMapper — bidirectional mapping between the wire-format
// sealed-record hierarchy (WorkloadKind) and its string form ("vm",
// "docker-compose", ...). The control plane stores Kind as a varchar
// in forge.workloads; the NodeAgent dispatches on the string when
// it receives a command envelope; the wire JSON itself uses the
// string via WorkloadKind.Name.
//
// All conversions are total over the currently-defined kinds
// (Vm, Lxc, Qemu, K8sPod, Container, DockerCompose, PodmanQuadlet,
// K3s). FromName throws NotSupportedException on unknown strings
// — callers should treat that as an operator error (the kind was
// removed from the wire contract since the row was written).
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Bidirectional mapping between <see cref="WorkloadKind" />
///     sealed records and their wire-format strings. The mapping
///     is total over the closed set of kinds defined in the
///     <see cref="WorkloadKind" /> hierarchy; FromName throws on
///     unknown values (forward-compatible: a newer agent sending
///     an unknown kind to an older control plane surfaces as a
///     not-supported exception in the dispatch path).
/// </summary>
public static class WorkloadKindMapper
{
    /// <summary>
    ///     Resolve the wire-format name (<see cref="WorkloadKind.Name" />)
    ///     of a <see cref="WorkloadKind" /> sealed record back to a
    ///     matching sealed record instance. Returns null when the
    ///     input is null (callers can treat null Kind as
    ///     "unset").
    /// </summary>
    /// <param name="name">Wire-format kind name (e.g. <c>"docker-compose"</c>).</param>
    /// <returns>The matching sealed record, or null on null input.</returns>
    /// <exception cref="NotSupportedException">
    ///     The wire name does not match any defined kind.
    /// </exception>
    public static WorkloadKind? FromName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        return name switch
        {
            "vm" => new WorkloadKind.Vm(),
            "lxc" => new WorkloadKind.Lxc(),
            "qemu" => new WorkloadKind.Qemu(),
            "k8s.pod" => new WorkloadKind.K8sPod(),
            "container" => new WorkloadKind.Container(),
            "docker-compose" => new WorkloadKind.DockerCompose(),
            "podman-quadlet" => new WorkloadKind.PodmanQuadlet(),
            "k3s" => new WorkloadKind.K3s(),
            _ => throw new NotSupportedException(
                $"Unknown workload kind '{name}'. " +
                $"Supported: vm, lxc, qemu, k8s.pod, container, " +
                $"docker-compose, podman-quadlet, k3s."),
        };
    }

    /// <summary>
    ///     Convert a <see cref="WorkloadKind" /> to its wire-format
    ///     name. Symmetric to <see cref="FromName" />: round-trips
    ///     preserve equality.
    /// </summary>
    /// <param name="kind">Sealed record instance.</param>
    /// <returns>The wire-format name.</returns>
    public static string ToName(WorkloadKind kind)
    {
        return kind.Name;
    }
}
