// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Unit tests for NodeProbe — pins the cross-platform detection
// rules. The probe is best-effort and never throws, so tests
// focus on:
//
//   - what the probe reports for the current test host
//     (a property assertion rather than a hard-coded
//     expectation, since CI may run on Windows / Linux / macOS)
//   - the report shape (ToString, JSON round-trip)
//   - the "host" overlay is always present (every node
//     has at least the kernel bridge)
//   - the probedAt timestamp is recent and UTC
// ============================================================================

using System.Text.Json;
using Plexor.Shared.Capabilities;
using Shouldly;

namespace Plexor.Shared.Capabilities.Unit;

public sealed class NodeProbeTests
{
    /// <summary>
    ///     The probe must always include the "host" overlay —
    ///     every node has at least a kernel bridge, even if no
    ///     OVS / Cilium is installed.
    /// </summary>
    [Fact]
    public void Always_reports_host_overlay()
    {
        var caps = NodeProbe.Detect();

        caps.Network.ShouldContain("host");
    }

    /// <summary>
    ///     <c>ProbedAt</c> is a fresh UTC timestamp, not
    ///     something from 1970 or unset.
    /// </summary>
    [Fact]
    public void Probed_at_is_recent_utc()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var caps = NodeProbe.Detect();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        caps.ProbedAt.ShouldBeInRange(before, after);
        caps.ProbedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>
    ///     JSON round-trip preserves all fields. The probe
    ///     sends this over the wire to the control plane, so
    ///     any serialization loss is a contract bug.
    /// </summary>
    [Fact]
    public void Round_trips_through_json()
    {
        var original = new NodeCapabilities
        {
            Compute = ["vm", "lxc"],
            Network = ["ovs", "host"],
            Storage = ["local-lvm"],
            NestedVirt = true,
            K3sServer = false,
            ProbedAt = DateTimeOffset.UtcNow,
            Label = "node-b-eu-1",
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<NodeCapabilities>(json);

        restored.ShouldNotBeNull();
        restored!.Compute.ShouldBe(["vm", "lxc"], ignoreOrder: true);
        restored.Network.ShouldBe(["ovs", "host"], ignoreOrder: true);
        restored.Storage.ShouldBe(["local-lvm"]);
        restored.NestedVirt.ShouldBeTrue();
        restored.K3sServer.ShouldBeFalse();
        restored.Label.ShouldBe("node-b-eu-1");
    }

    /// <summary>
    ///     Compact <see cref="NodeCapabilities.ToString" /> is
    ///     what the NodeAgent logs on probe completion. Assert
    ///     it includes at minimum the host-overlay marker so
    ///     the log line is non-empty (operator sanity check).
    /// </summary>
    [Fact]
    public void ToString_contains_network_marker()
    {
        var caps = NodeProbe.Detect();

        caps.ToString().ShouldContain("network=host");
    }

    /// <summary>
    ///     When nested virt isn't possible (no /dev/kvm or no
    ///     vmx/svm flag), the probe must report
    ///     <c>NestedVirt = false</c>. We assert that on the test
    ///     host, the value is consistent with what the
    ///     underlying probes say (kvm-present ↔ nestedVirt=true).
    /// </summary>
    [Fact]
    public void NestedVirt_consistent_with_kvm_presence()
    {
        var caps = NodeProbe.Detect();

        if (caps.Compute.Contains("vm"))
        {
            caps.NestedVirt.ShouldBeTrue(
                "Compute includes 'vm' but NestedVirt is false — " +
                "the probe sees a /dev/kvm but the CPU lacks vmx/svm");
        }
        else
        {
            caps.NestedVirt.ShouldBeFalse(
                "Compute does NOT include 'vm' but NestedVirt is true — " +
                "NestedVirt should be false when KVM is unavailable");
        }
    }

    /// <summary>
    ///     A probe running on a fresh node returns *some*
    ///     capabilities (not all empty). If the probe returns
    ///     nothing across the board that's a sign the node
    ///     is misconfigured (no kernel modules loaded, no
    ///     runtimes installed, etc.) and the operator should
    ///     investigate before joining.
    /// </summary>
    [Fact]
    public void Probe_returns_at_least_one_capability()
    {
        var caps = NodeProbe.Detect();

        var total = caps.Compute.Count + caps.Network.Count + caps.Storage.Count;
        total.ShouldBeGreaterThan(0,
            "probe returned no capabilities at all — " +
            "either the test host has nothing installed, " +
            "or the probe is broken");
    }
}