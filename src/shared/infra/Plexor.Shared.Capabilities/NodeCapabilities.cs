// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCapabilities — structured per-node capability record.
//
// Replaces NodeSpec.Providers[] (a free-form string[] that didn't
// distinguish compute vs network vs storage, and didn't tell the
// scheduler what nested-virt looked like). Three flat
// HashSet<string> for runtime flags plus a few booleans for
// binary capabilities that the probe can detect directly.
//
// String values match the runtime identifiers used elsewhere
// (see runtimes-and-bindings.md §Runtime — the same vocabulary):
//
//   Compute: "vm" (=KVM/QEMU), "lxc", "docker", "k3s"
//   Network: "ovs", "cilium", "host" (bridge-only)
//   Storage: "ceph-rbd", "local-lvm", "longhorn"
//
// A capability is *present* if its string is in the set; absence
// means the probe couldn't see it (or operator excluded it). An
// empty set means "this node has no compute / network / storage".
// ============================================================================

using System.Text.Json.Serialization;

namespace Plexor.Shared.Capabilities;

/// <summary>
///     Structured per-node capability record. Produced by
///     <see cref="NodeProbe" /> on the agent side, persisted in
///     <c>node.yaml</c> and forwarded to the control plane via
///     <c>POST /api/v1/nodes/{id}/capabilities</c>. The control
///     plane's scheduler intersects these with each service's
///     <c>validRuntimes</c> to pick a placement.
/// </summary>
public sealed record NodeCapabilities
{
    /// <summary>
    ///     Compute runtimes the node can host. Empty set = "no
    ///     workloads can run here" (control plane that doesn't
    ///     host workloads).
    /// </summary>
    [JsonPropertyName("compute")]
    public IReadOnlyCollection<string> Compute { get; init; } = [];

    /// <summary>
    ///     Network capabilities (overlay providers). Empty set =
    ///     "no overlay networking" (lame node, only bridge).
    /// </summary>
    [JsonPropertyName("network")]
    public IReadOnlyCollection<string> Network { get; init; } = [];

    /// <summary>
    ///     Storage backends this node can provide. Empty set =
    ///     "ephemeral only" (no persistent volumes on this node).
    /// </summary>
    [JsonPropertyName("storage")]
    public IReadOnlyCollection<string> Storage { get; init; } = [];

    /// <summary>
    ///     True if the node can run VMs that themselves host
    ///     KVM-accelerated workloads. False on cloud VMs, inside
    ///     LXC, or when /dev/kvm isn't exposed. The control plane
    ///     downgrades "vm" → "lxc"/"docker" automatically when
    ///     this is false.
    /// </summary>
    [JsonPropertyName("nestedVirt")]
    public bool NestedVirt { get; init; }

    /// <summary>
    ///     True if the node runs a K3s server (control-plane role
    ///     for k3s-runtime workloads). Exactly one node per cluster
    ///     should have this true; the k3s installer toggles it.
    /// </summary>
    [JsonPropertyName("k3sServer")]
    public bool K3sServer { get; init; }

    /// <summary>
    ///     Detected at probe time. Lets the operator know the
    ///     capability report is based on real probes, not
    ///     operator-provided stub data.
    /// </summary>
    [JsonPropertyName("probedAt")]
    public DateTimeOffset ProbedAt { get; init; }

    /// <summary>
    ///     Operator-friendly label ("node-b-eu-central-1", "edge-
    ///     gateway-01"). Optional; the control plane falls back
    ///     to hostname if absent.
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>
    ///     Pretty-print for logs / dashboard. "compute=vm,lxc;
    ///     network=ovs; storage=local-lvm; nestedVirt=true"
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Compute.Count > 0)
        {
            parts.Add($"compute={string.Join(",", Compute)}");
        }
        if (Network.Count > 0)
        {
            parts.Add($"network={string.Join(",", Network)}");
        }
        if (Storage.Count > 0)
        {
            parts.Add($"storage={string.Join(",", Storage)}");
        }
        if (NestedVirt)
        {
            parts.Add("nestedVirt");
        }
        if (K3sServer)
        {
            parts.Add("k3sServer");
        }
        if (Label is not null)
        {
            parts.Add($"label={Label}");
        }
        return string.Join("; ", parts);
    }
}