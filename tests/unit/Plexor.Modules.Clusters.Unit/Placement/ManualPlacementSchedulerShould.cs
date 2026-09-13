// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ManualPlacementSchedulerShould — unit tests for the v0.1
// placement scheduler. The manual policy only honors the operator's
// pin; the scheduler never picks a node on its own. Tests cover:
//  - Pin set + node in candidates → returns the pinned node
//  - Pin set + node NOT in candidates → null (stale id rejected)
//  - Pin unset + candidates present → null (operator must decide)
//  - Candidates empty → null
//  - Pin set + candidates empty → null
//  - MarkAssignedAsync / ReleaseAsync — no-op, never throw
// ==========================================================================

using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Infrastructure.Placement;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Placement;

public sealed class ManualPlacementSchedulerShould
{
    [Fact(DisplayName = "Given pin set and candidate present, when SelectNodeAsync, then returns pinned node")]
    public async Task ReturnsPinnedNodeWhenPresentAsync()
    {
        var sut = new ManualPlacementScheduler();
        var pinnedNode = new NodeId(Guid.NewGuid());
        var candidates = new List<NodeCandidate>
        {
            new(pinnedNode, "node-a", [], 0, 0, 0),
            new(new NodeId(Guid.NewGuid()), "node-b", [], 0, 0, 0),
        };
        var spec = new WorkloadSpec(
            ClusterId: new ClusterId(Guid.NewGuid()),
            Name: "web-1",
            Kind: "container",
            TargetNodeId: pinnedNode,
            RequiredCapabilities: []);

        var result = await sut.SelectNodeAsync(spec, candidates, CancellationToken.None);

        result.ShouldBe(pinnedNode);
    }

    [Fact(DisplayName = "Given pin set but candidate list empty, when SelectNodeAsync, then returns null")]
    public async Task ReturnNullWhenPinSetButCandidatesEmptyAsync()
    {
        var sut = new ManualPlacementScheduler();
        var spec = new WorkloadSpec(
            ClusterId: new ClusterId(Guid.NewGuid()),
            Name: "web-1",
            Kind: "container",
            TargetNodeId: new NodeId(Guid.NewGuid()),
            RequiredCapabilities: []);

        var result = await sut.SelectNodeAsync(spec, [], CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "Given pin set but candidate not in list, when SelectNodeAsync, then returns null")]
    public async Task ReturnNullWhenPinNotInCandidatesAsync()
    {
        var sut = new ManualPlacementScheduler();
        var candidates = new List<NodeCandidate>
        {
            new(new NodeId(Guid.NewGuid()), "node-a", [], 0, 0, 0),
        };
        var spec = new WorkloadSpec(
            ClusterId: new ClusterId(Guid.NewGuid()),
            Name: "web-1",
            Kind: "container",
            TargetNodeId: new NodeId(Guid.NewGuid()),
            RequiredCapabilities: []);

        var result = await sut.SelectNodeAsync(spec, candidates, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "Given pin unset, when SelectNodeAsync, then returns null (manual policy never picks)")]
    public async Task ReturnNullWhenPinUnsetAsync()
    {
        var sut = new ManualPlacementScheduler();
        var candidates = new List<NodeCandidate>
        {
            new(new NodeId(Guid.NewGuid()), "node-a", [], 0, 0, 0),
            new(new NodeId(Guid.NewGuid()), "node-b", [], 0, 0, 0),
        };
        var spec = new WorkloadSpec(
            ClusterId: new ClusterId(Guid.NewGuid()),
            Name: "web-1",
            Kind: "container",
            TargetNodeId: null,
            RequiredCapabilities: []);

        var result = await sut.SelectNodeAsync(spec, candidates, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "Given MarkAssignedAsync, when called, completes without throwing")]
    public async Task MarkAssignedAsyncCompletesAsync()
    {
        var sut = new ManualPlacementScheduler();

        await sut.MarkAssignedAsync(
            new WorkloadId(Guid.NewGuid()),
            new NodeId(Guid.NewGuid()),
            CancellationToken.None);
    }

    [Fact(DisplayName = "Given ReleaseAsync, when called, completes without throwing")]
    public async Task ReleaseAsyncCompletesAsync()
    {
        var sut = new ManualPlacementScheduler();

        await sut.ReleaseAsync(
            new WorkloadId(Guid.NewGuid()),
            CancellationToken.None);
    }
}
