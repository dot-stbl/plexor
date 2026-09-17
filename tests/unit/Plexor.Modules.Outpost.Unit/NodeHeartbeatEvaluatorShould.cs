// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHeartbeatEvaluatorShould — exercise the staleness/health
// classifier. Pure function: takes last-heartbeat + threshold
// windows, returns NodeHealth.
//
// Three threshold windows:
//   * lastHeartbeatAt == null                  → Unhealthy
//   * age <= HealthyWindow                     → Healthy
//   * HealthyWindow < age <= UnhealthyWindow   → Stale
//   * age > UnhealthyWindow                     → Unhealthy
// ============================================================================

using Microsoft.Extensions.Options;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Modules.Outpost.Infrastructure.Nodes;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Outpost.Unit;

public sealed class NodeHeartbeatEvaluatorShould
{
    private static readonly DateTimeOffset TestNow = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static NodeHeartbeatEvaluator CreateEvaluator(TimeSpan? healthy = null, TimeSpan? unhealthy = null)
    {
        var options = Options.Create(new OutpostHeartbeatOptions
        {
            HealthyWindow = healthy ?? TimeSpan.FromSeconds(30),
            UnhealthyWindow = unhealthy ?? TimeSpan.FromSeconds(90),
        });
        return new NodeHeartbeatEvaluator(options);
    }

    [Fact(DisplayName = "Given never-heartbeated node, when Evaluate, then returns Unhealthy")]
    public void NeverHeartbeatedIsUnhealthy()
    {
        var nodeId = IdGenerator.NewNodeId();
        var sut = CreateEvaluator();

        sut.Evaluate(nodeId, null, TestNow).ShouldBe(NodeHealth.Unhealthy);
    }

    [Fact(DisplayName = "Given fresh heartbeat (age=10s, window=30s), when Evaluate, then returns Healthy")]
    public void FreshHeartbeatIsHealthy()
    {
        var nodeId = IdGenerator.NewNodeId();
        var sut = CreateEvaluator();
        var lastHeartbeat = TestNow.AddSeconds(-10);

        sut.Evaluate(nodeId, lastHeartbeat, TestNow).ShouldBe(NodeHealth.Healthy);
    }

    [Fact(DisplayName = "Given stale heartbeat (age=60s, healthy=30s, unhealthy=90s), when Evaluate, then returns Stale")]
    public void StaleHeartbeatIsStale()
    {
        var nodeId = IdGenerator.NewNodeId();
        var sut = CreateEvaluator();
        var lastHeartbeat = TestNow.AddSeconds(-60);

        sut.Evaluate(nodeId, lastHeartbeat, TestNow).ShouldBe(NodeHealth.Stale);
    }

    [Fact(DisplayName = "Given ancient heartbeat (age=120s, unhealthy=90s), when Evaluate, then returns Unhealthy")]
    public void AncientHeartbeatIsUnhealthy()
    {
        var nodeId = IdGenerator.NewNodeId();
        var sut = CreateEvaluator();
        var lastHeartbeat = TestNow.AddSeconds(-120);

        sut.Evaluate(nodeId, lastHeartbeat, TestNow).ShouldBe(NodeHealth.Unhealthy);
    }
}