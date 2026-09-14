// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeAgentControllerShould — verify NodeAgentController dispatches
// /join and /heartbeat to the right ICommandHandler<...> and returns
// 200 Ok with the handler's result. Uses NSubstitute per
// testing-unit.md §7 — no DbContext.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Plexor.Modules.Clusters.Api.Controllers;
using Plexor.Modules.Clusters.Api.Models;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Api;

public sealed class NodeAgentControllerShould
{
    [Fact(DisplayName = "Given a join request, when JoinAsync is called, then dispatches NodeJoinCommand and returns 200 Ok")]
    public async Task JoinAsyncDispatchesAndReturnsOkAsync()
    {
        var joinHandler = Substitute.For<ICommandHandler<NodeJoinCommand, NodeJoinResult>>();
        var heartbeatHandler = Substitute.For<ICommandHandler<NodeHeartbeatCommand, NodeHeartbeatResult>>();
        var sut = new NodeAgentController(joinHandler, heartbeatHandler);

        var nodeId = IdGenerator.NewNodeId();
        var clusterId = IdGenerator.NewClusterId();
        var expected = new NodeJoinResult(
            NodeId: nodeId,
            ClusterId: clusterId,
            NodeToken: "node-token",
            ControlPlaneUrl: "https://cp.example.com",
            WireguardConfig: "wg-config-blob",
            NodeCertificatePem: "cert-pem",
            NodePrivateKeyPem: "key-pem",
            CaCertificatePem: "ca-pem");
        joinHandler.HandleAsync(Arg.Any<NodeJoinCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new NodeJoinRequest(
            JoinToken: "join-token",
            Hostname: "host-1",
            Role: NodeRole.Control,
            Hardware: new NodeHardware(Vcpu: 4, RamGb: 16, DiskGb: 100, Providers: []));

        var actionResult = await sut.JoinAsync(request, CancellationToken.None);

        actionResult.ShouldNotBeNull();
        var ok = actionResult.Result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ok.Value.ShouldBe(expected);

        await joinHandler.Received(1).HandleAsync(
            Arg.Is<NodeJoinCommand>(
                static command => command.JoinToken == "join-token"
                           && command.Hostname == "host-1"
                           && command.Role == NodeRole.Control),
            Arg.Any<CancellationToken>());
        await heartbeatHandler.DidNotReceive().HandleAsync(
            Arg.Any<NodeHeartbeatCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a heartbeat request, when HeartbeatAsync is called, then dispatches NodeHeartbeatCommand and returns 200 Ok")]
    public async Task HeartbeatAsyncDispatchesAndReturnsOkAsync()
    {
        var joinHandler = Substitute.For<ICommandHandler<NodeJoinCommand, NodeJoinResult>>();
        var heartbeatHandler = Substitute.For<ICommandHandler<NodeHeartbeatCommand, NodeHeartbeatResult>>();
        var sut = new NodeAgentController(joinHandler, heartbeatHandler);

        var nodeId = IdGenerator.NewNodeId();
        var clusterId = IdGenerator.NewClusterId();
        var serverTime = DateTimeOffset.UtcNow;
        var expected = new NodeHeartbeatResult(
            NodeId: nodeId,
            ClusterStatus: ClusterStatus.Ready,
            ServerTime: serverTime);
        heartbeatHandler.HandleAsync(Arg.Any<NodeHeartbeatCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new NodeHeartbeatRequest(
            NodeId: nodeId.ToString(),
            Hardware: new NodeHardware(Vcpu: 2, RamGb: 8, DiskGb: 50, Providers: []),
            Reports: []);

        var actionResult = await sut.HeartbeatAsync(clusterId, request, CancellationToken.None);

        actionResult.ShouldNotBeNull();
        var ok = actionResult.Result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ok.Value.ShouldBe(expected);

        await heartbeatHandler.Received(1).HandleAsync(
            Arg.Is<NodeHeartbeatCommand>(
                command => command.ClusterId == clusterId
                           && command.NodeId == nodeId
                           && command.Hardware.Vcpu == 2),
            Arg.Any<CancellationToken>());
        await joinHandler.DidNotReceive().HandleAsync(
            Arg.Any<NodeJoinCommand>(), Arg.Any<CancellationToken>());
    }
}
