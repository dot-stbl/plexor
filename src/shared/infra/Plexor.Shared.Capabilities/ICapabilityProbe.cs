// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ICapabilityProbe — the contract every provider implements to tell
// the cluster "I can run X". The aggregator runs all registered
// probes in parallel and unions their results.
//
// Why per-provider: each runtime (Docker, KVM, LXC, OVS, K3s) is
// its own product with its own domain knowledge. KVM knows about
// /dev/kvm and libvirtd; OVS knows about ovs-vsctl; K3s knows about
// the k3s service. Packing that into a shared static class turned
// the probe into a 10 KB file with 8 private methods — the §9
// procedural-class anti-pattern, and the wrong shape for the
// domain (a runtime provider owns its own detection logic).
//
// New providers register their probe in DI:
//   services.AddTransient<ICapabilityProbe, DockerCapabilityProbe>();
// The aggregator picks them up automatically.
// ============================================================================

namespace Plexor.Shared.Capabilities;

/// <summary>
///     Detects the capabilities a single runtime / overlay / storage
///     backend contributes on the current node. Implementations live
///     in the provider's own assembly (e.g. DockerCapabilityProbe in
///     Plexor.Providers.Compute.Docker) so the domain knowledge
///     stays with the runtime that owns the rest of the integration.
/// </summary>
public interface ICapabilityProbe
{
    /// <summary>
    ///     Short stable identifier for the probe — "docker",
    ///     "kvm-runtime", "ovs-overlay", "host-bridge". Used in
    ///     logs and as a key in capability-flavoured feature flags
    ///     (e.g. <c>capability:host-bridge</c>).
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    ///     Return the set of capability names this provider confirms
    ///     are available on the current node. Empty set = "I checked
    ///     and there's nothing to advertise" (not the same as
    ///     "I failed to check" — failures are reported via
    ///     <see cref="System.Threading.Tasks.Task" /> exception
    ///     propagation, not by an empty result).
    /// </summary>
    /// <remarks>
    ///     The standard capability vocabulary is documented in
    ///     <c>runtime-capabilities-networking.md</c>. New
    ///     capabilities should be added there first, then
    ///     implemented in the relevant provider's probe.
    /// </remarks>
    public Task<IReadOnlyCollection<string>> ProbeAsync(CancellationToken cancellationToken = default);
}