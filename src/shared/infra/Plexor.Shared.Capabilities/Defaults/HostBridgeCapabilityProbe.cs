// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostBridgeCapabilityProbe — the always-available capability probe.
// Every node has at least a kernel bridge; that's the
// "host-bridge" capability. It signals to the scheduler that the
// node can host workloads on the basic Linux bridge (no overlay,
// no OVS) — useful for dev / single-tenant clusters that don't
// need VXLAN.
//
// Lives in the shared assembly because (a) it's trivial and
// (b) every node has it. Real overlay providers (OvS, Cilium)
// ship their own probes that *add* capabilities on top of this
// baseline; the aggregator unions all of them.
// ============================================================================

namespace Plexor.Shared.Capabilities.Defaults;

/// <summary>
///     Always-available baseline probe — advertises
///     <c>host-bridge</c> regardless of which overlay providers
///     are installed. The aggregator unions this with whatever
///     the runtime-specific probes contribute.
/// </summary>
public sealed class HostBridgeCapabilityProbe : ICapabilityProbe
{
    /// <inheritdoc />
    public string ProviderName => "host-bridge";

    /// <inheritdoc />
    public Task<IReadOnlyCollection<string>> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        // No probing needed — the host bridge is a kernel
        // primitive that's available on every Linux host. We
        // still go through the ICapabilityProbe contract so the
        // aggregator has a uniform shape to call.
        IReadOnlyCollection<string> result = ["host-bridge"];
        return Task.FromResult(result);
    }
}
