// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHardwareSpec — wire-shape hardware snapshot reported by
// Plexor.NodeAgent on every join and heartbeat. Distinct from
// Plexor.Modules.Outpost.Application.NodeSpec (the domain value object)
// so the wire shape can evolve independently of the persisted column —
// e.g. add a GPU-count field before the domain does. Moved here from
// Plexor.Modules.Outpost.Api.Models in the NodeAgent wire-format
// alignment (Sep 2026) so the agent doesn't have to reference
// host-side types.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Wire shape for the hardware snapshot in <c>POST /nodes/register</c>
///     and <c>POST /nodes/heartbeat</c> bodies. Distinct from the
///     domain's <c>NodeSpec</c> so the wire shape can evolve
///     independently (e.g. add a GPU-count field before the domain does).
/// </summary>
/// <param name="Vcpu">Logical CPU count visible to the kernel.</param>
/// <param name="RamGb">Total RAM in gibibytes (rounded up).</param>
/// <param name="DiskGb">Total block storage in gibibytes reachable from the node.</param>
/// <param name="Providers">Install providers present on this node (kvm, lxc, pod, ovs, cilium, ...).</param>
public sealed record NodeHardwareSpec(
    int Vcpu,
    int RamGb,
    int DiskGb,
    IReadOnlyList<string> Providers);