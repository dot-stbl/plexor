// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodesControllerShould — exercise the controller surface in isolation
// via NSubstitute mocks for every ICommandHandler dependency + the
// INodeHeartbeatEvaluator + TimeProvider. No DbContext, no HTTP
// pipeline — the controller's job is to delegate to handlers; this
// test pins that delegation shape (correct handler call + correct
// response shape).
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Outpost.Api.Controllers;
using Plexor.Modules.Outpost.Api.Models;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Modules.Outpost.Application.NodeCommands;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Outpost.Unit;

public sealed class NodesControllerShould
{
    private static readonly DateTimeOffset TestNow = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly FakeTimeProvider Clock = new(TestNow);

    private static (NodesController Sut,
                    ICommandHandler<RegisterNodeCommand, RegisterNodeResult> Register,
                    ICommandHandler<HeartbeatCommand, HeartbeatResult> Heartbeat,
                    ICommandHandler<ListNodesQuery, IReadOnlyList<NodeRecord>> List,
                    ICommandHandler<GetNodeQuery, NodeRecord?> Get,
                    INodeHeartbeatEvaluator Evaluator)
        Build()
    {
        var register = Substitute.For<ICommandHandler<RegisterNodeCommand, RegisterNodeResult>>();
        var heartbeat = Substitute.For<ICommandHandler<HeartbeatCommand, HeartbeatResult>>();
        var list = Substitute.For<ICommandHandler<ListNodesQuery, IReadOnlyList<NodeRecord>>>();
        var get = Substitute.For<ICommandHandler<GetNodeQuery, NodeRecord?>>();
        var evaluator = Substitute.For<INodeHeartbeatEvaluator>();
        var sut = new NodesController(register, heartbeat, list, get, evaluator, Clock);
        return (sut, register, heartbeat, list, get, evaluator);
    }

    private static NodeRecord NewNode(ClusterId clusterId)
    {
        return new NodeRecord
        {
            Id = IdGenerator.NewNodeId(),
            ClusterId = clusterId,
            OrgId = Guid.NewGuid(),
            Hostname = "node-1",
            IpAddress = "10.0.0.1",
            Role = NodeRole.Compute,
            Status = NodeStatus.Ready,
            Spec = new NodeSpec(4, 16, 100, []),
            CreatedAt = TestNow.AddMinutes(-5),
            UpdatedAt = TestNow.AddMinutes(-1),
            LastHeartbeatAt = TestNow.AddSeconds(-30),
        };
    }

    [Fact(DisplayName = "Given cluster with 2 nodes, when ListAsync, then calls handler + returns NodeListResponse")]
    public async Task ListReturnsNodes()
    {
        var (sut, _, _, list, _, _) = Build();
        var clusterId = IdGenerator.NewClusterId();
        var node1 = NewNode(clusterId);
        var node2 = NewNode(clusterId);
        list.HandleAsync(Arg.Any<ListNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new[] { node1, node2 });

        var result = await sut.ListAsync(clusterId.ToString(), CancellationToken.None);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<NodeListResponse>();
        response.Nodes.Count.ShouldBe(2);
        await list.Received(1).HandleAsync(
            Arg.Is<ListNodesQuery>(q => q.ClusterId == clusterId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given existing node id, when GetAsync, then returns 200 + NodeResponse")]
    public async Task GetReturnsNode()
    {
        var (sut, _, _, _, get, _) = Build();
        var node = NewNode(IdGenerator.NewClusterId());
        get.HandleAsync(Arg.Any<GetNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(node);

        var result = await sut.GetAsync(node.Id, CancellationToken.None);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<NodeResponse>();
        response.Hostname.ShouldBe("node-1");
    }

    [Fact(DisplayName = "Given missing node id, when GetAsync, then returns 404")]
    public async Task GetReturns404ForMissing()
    {
        var (sut, _, _, _, get, _) = Build();
        get.HandleAsync(Arg.Any<GetNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns((NodeRecord?)null);

        var result = await sut.GetAsync(IdGenerator.NewNodeId(), CancellationToken.None);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact(DisplayName = "Given valid register request, when RegisterAsync, then calls handler with mapped NodeSpec")]
    public async Task RegisterDispatchesHandler()
    {
        var (sut, register, _, _, _, _) = Build();
        var nodeId = IdGenerator.NewNodeId();
        var clusterId = IdGenerator.NewClusterId();
        var baseNode = NewNode(clusterId);
        var node = new NodeRecord
        {
            Id = nodeId,
            ClusterId = clusterId,
            OrgId = baseNode.OrgId,
            Hostname = baseNode.Hostname,
            IpAddress = baseNode.IpAddress,
            Role = baseNode.Role,
            Status = baseNode.Status,
            Spec = baseNode.Spec,
            CreatedAt = baseNode.CreatedAt,
            UpdatedAt = baseNode.UpdatedAt,
            LastHeartbeatAt = baseNode.LastHeartbeatAt,
        };
        register.HandleAsync(Arg.Any<RegisterNodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RegisterNodeResult(node, "node-token", "https://plexor.host"));

        var result = await sut.RegisterAsync(
            new RegisterNodeRequest(
                "join-token",
                "node-1",
                "10.0.0.1",
                NodeRole.Compute,
                new NodeHardwareSpec(4, 16, 100, ["kvm"]),
                "0.1.0-dev",
                string.Empty),
            CancellationToken.None);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<RegisterNodeResponse>();
        await register.Received(1).HandleAsync(
            Arg.Is<RegisterNodeCommand>(c =>
                c.Hostname == "node-1"
                && c.IpAddress == "10.0.0.1"
                && c.Role == NodeRole.Compute
                && c.Spec.Vcpu == 4
                && c.Spec.Providers.Count == 1
                && c.Spec.Providers[0] == "kvm"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given valid heartbeat request, when HealthAsync, then returns derived NodeHealth")]
    public async Task HealthReturnsDerivedStatus()
    {
        var (sut, _, _, _, get, evaluator) = Build();
        var node = NewNode(IdGenerator.NewClusterId());
        get.HandleAsync(Arg.Any<GetNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(node);
        evaluator.Evaluate(Arg.Any<NodeId>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>())
            .Returns(NodeHealth.Stale);

        var result = await sut.HealthAsync(node.Id, CancellationToken.None);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<NodeHealthResponse>();
        response.Health.ShouldBe(NodeHealth.Stale);
        response.NodeId.ShouldBe(node.Id);
    }
}

/// <summary>Minimal TimeProvider for unit tests. Returns the supplied UtcNow.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset now;

    public FakeTimeProvider(DateTimeOffset now)
    {
        this.now = now;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return now;
    }
}