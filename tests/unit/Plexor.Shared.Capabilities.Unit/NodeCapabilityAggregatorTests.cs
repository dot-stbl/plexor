// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Unit tests for the capability detection architecture — pins
// the ICapabilityProbe contract, the aggregator's union
// semantics, and the "one failing probe doesn't kill the report"
// guarantee. The shared project ships one baseline probe
// (HostBridgeCapabilityProbe); runtime-specific probes (Docker,
// KVM, LXC, OVS, K3s) live in their own provider projects and
// aren't tested here — those tests belong in the provider's repo.
// ============================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Plexor.Shared.Capabilities;
using Plexor.Shared.Capabilities.Defaults;
using Shouldly;

namespace Plexor.Shared.Capabilities.Unit;

public sealed class NodeCapabilityAggregatorTests
{
    /// <summary>
    ///     Single empty aggregator returns an empty report
    ///     (degenerate but possible — node with no probes
    ///     registered).
    /// </summary>
    [Fact]
    public async Task Empty_probes_yields_empty_report()
    {
        var aggregator = new NodeCapabilityAggregator(
            Array.Empty<ICapabilityProbe>(),
            NullLogger<NodeCapabilityAggregator>.Instance);

        var report = await aggregator.AggregateAsync();

        report.Capabilities.ShouldBeEmpty();
    }

    /// <summary>
    ///     One probe's set is returned verbatim.
    /// </summary>
    [Fact]
    public async Task Single_probe_contributes_its_capabilities()
    {
        var aggregator = new NodeCapabilityAggregator(
            [new FixedProbe("docker", ["docker-runtime", "host-bridge"])],
            NullLogger<NodeCapabilityAggregator>.Instance);

        var report = await aggregator.AggregateAsync();

        report.Capabilities.ShouldBe(
            ["docker-runtime", "host-bridge"],
            ignoreOrder: false);
    }

    /// <summary>
    ///     Overlapping capability sets across multiple probes
    ///     get de-duplicated by the aggregator — a capability
    ///     reported by two probes still appears once in the
    ///     final report. The SortedSet is the source of truth
    ///     here, so duplicates collapse automatically.
    /// </summary>
    [Fact]
    public async Task Overlapping_probes_are_unioned_not_duplicated()
    {
        var aggregator = new NodeCapabilityAggregator(
            [
                new FixedProbe("docker", ["docker-runtime", "host-bridge"]),
                new FixedProbe("ovs", ["ovs-overlay", "host-bridge"]),
            ],
            NullLogger<NodeCapabilityAggregator>.Instance);

        var report = await aggregator.AggregateAsync();

        report.Capabilities.ShouldBe(
            ["docker-runtime", "host-bridge", "ovs-overlay"],
            ignoreOrder: false);
    }

    /// <summary>
    ///     A throwing probe doesn't kill the whole report — its
    ///     contribution is skipped, a warning is logged, the
    ///     other probes still contribute. This is the core
    ///     failure-mode guarantee the aggregator exists to
    ///     provide: a permissions issue on /dev/kvm (which
    ///     KVM probe can hit) shouldn't take down a perfectly
    ///     valid Docker capability report.
    /// </summary>
    [Fact]
    public async Task Throwing_probe_does_not_kill_the_report()
    {
        var aggregator = new NodeCapabilityAggregator(
            [
                new FixedProbe("docker", ["docker-runtime"]),
                new ThrowingProbe("kvm", "permission denied on /dev/kvm"),
            ],
            NullLogger<NodeCapabilityAggregator>.Instance);

        var report = await aggregator.AggregateAsync();

        report.Capabilities.ShouldBe(["docker-runtime"]);
    }

    /// <summary>
    ///     The default baseline probe (HostBridgeCapabilityProbe)
    ///     advertises "host-bridge" — that's the contract every
    ///     node relies on even before any runtime-specific probe
    ///     is registered. The aggregator picks it up
    ///     automatically when DI registers it.
    /// </summary>
    [Fact]
    public async Task Baseline_probe_advertises_host_bridge()
    {
        var aggregator = new NodeCapabilityAggregator(
            [new HostBridgeCapabilityProbe()],
            NullLogger<NodeCapabilityAggregator>.Instance);

        var report = await aggregator.AggregateAsync();

        report.Capabilities.ShouldBe(["host-bridge"]);
    }

    /// <summary>
    ///     The report's ProbedAt is recent UTC — useful for the
    ///     control plane to detect a stale NodeAgent and re-probe.
    /// </summary>
    [Fact]
    public async Task Probed_at_is_fresh_utc()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var aggregator = new NodeCapabilityAggregator(
            [new HostBridgeCapabilityProbe()],
            NullLogger<NodeCapabilityAggregator>.Instance);

        var report = await aggregator.AggregateAsync();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        report.ProbedAt.ShouldBeInRange(before, after);
        report.ProbedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>
    ///     The report's ToString is the shape the NodeAgent logs.
    ///     Stable across versions means operator log searches
    ///     keep working when we add fields.
    /// </summary>
    [Fact]
    public async Task Report_to_string_contains_capabilities_and_probed_at()
    {
        var aggregator = new NodeCapabilityAggregator(
            [new FixedProbe("docker", ["docker-runtime", "host-bridge"])],
            NullLogger<NodeCapabilityAggregator>.Instance);

        var report = await aggregator.AggregateAsync();

        report.ToString().ShouldContain("docker-runtime");
        report.ToString().ShouldContain("host-bridge");
        report.ToString().ShouldContain("probedAt=");
    }

    // -- test doubles ------------------------------------------------------

    /// <summary>
    ///     Always returns the same set. Lets tests assert exactly
    ///     what the aggregator does with a deterministic input
    ///     without depending on host state.
    /// </summary>
    private sealed class FixedProbe(
        string name,
        IReadOnlyCollection<string> capabilities) : ICapabilityProbe
    {
        public string ProviderName => name;

        public Task<IReadOnlyCollection<string>> ProbeAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(capabilities);
        }
    }

    /// <summary>
    ///     Always throws. Used to assert that a broken probe
    ///     doesn't kill the report.
    /// </summary>
    private sealed class ThrowingProbe(
        string name,
        string message) : ICapabilityProbe
    {
        public string ProviderName => name;

        public Task<IReadOnlyCollection<string>> ProbeAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }
    }
}
