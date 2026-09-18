// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmsControllerShould — verify VmsController dispatches POST /vms
// to the CreateVmCommandHandler and returns the expected 201
// CreatedAtAction. Uses NSubstitute per testing-unit.md §7 —
// no DbContext, no real handler. Mirrors the shape of
// WorkloadsControllerShould for the existing workloads endpoint.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Plexor.Modules.Clusters.Api.Controllers;
using Plexor.Modules.Clusters.Api.Models;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.CreateVm;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Api;

public sealed class VmsControllerShould
{
    [Fact(DisplayName = "Given a valid create request, when CreateAsync, then dispatches CreateVmCommand + returns 201 CreatedAtAction")]
    public async Task CreateAsyncDispatchesAndReturnsCreatedAtActionAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateVmCommand, CreateVmResult>>();
        var sut = new VmsController(createHandler);

        var clusterId = IdGenerator.NewClusterId();
        var nodeId = IdGenerator.NewNodeId();
        var workloadId = IdGenerator.NewWorkloadId();
        var expected = new CreateVmResult(workloadId, nodeId);
        createHandler.HandleAsync(Arg.Any<CreateVmCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new CreateVmRequest(
            ClusterId: clusterId,
            Name: "vm-alpha",
            Flavor: "small",
            Image: "ubuntu-22.04-cloud",
            Config: null,
            TargetNodeId: nodeId);

        var actionResult = await sut.CreateAsync(request, CancellationToken.None);

        var created = actionResult.Result.ShouldBeOfType<CreatedAtActionResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Value.ShouldBeOfType<CreateVmResponse>();
        var response = (CreateVmResponse)created.Value!;
        response.WorkloadId.ShouldBe(workloadId);
        response.AssignedNodeId.ShouldBe(nodeId);

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateVmCommand>(
                cmd => cmd.ClusterId == clusterId
                       && cmd.Name == "vm-alpha"
                       && cmd.FlavorName == "small"
                       && cmd.ImageName == "ubuntu-22.04-cloud"
                       && cmd.TargetNodeId == nodeId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given Flavor/Image omitted, when CreateAsync, then handler is invoked with null FlavorName / ImageName")]
    public async Task CreateAsyncDispatchesNullFlavorAndImageAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateVmCommand, CreateVmResult>>();
        var sut = new VmsController(createHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();
        createHandler.HandleAsync(Arg.Any<CreateVmCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateVmResult(workloadId, null));

        var request = new CreateVmRequest(
            ClusterId: clusterId,
            Name: "vm-default",
            Flavor: null,
            Image: null);

        var actionResult = await sut.CreateAsync(request, CancellationToken.None);

        var created = actionResult.Result.ShouldBeOfType<CreatedAtActionResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        var response = (CreateVmResponse)created.Value!;
        response.AssignedNodeId.ShouldBeNull();

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateVmCommand>(
                static cmd => cmd.FlavorName == null && cmd.ImageName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a Config overlay, when CreateAsync, then handler is invoked with the overlay verbatim")]
    public async Task CreateAsyncDispatchesConfigOverlayAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateVmCommand, CreateVmResult>>();
        var sut = new VmsController(createHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();
        createHandler.HandleAsync(Arg.Any<CreateVmCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateVmResult(workloadId, null));

        var overlay = new VmRuntimeConfig(
            Vcpu: 8,
            RamBytes: 16L * 1024 * 1024 * 1024,
            DiskBytes: 160L * 1024 * 1024 * 1024,
            ImageRef: "ubuntu-22.04-cloud",
            NetworkName: "prod-vpc",
            SshKeyFingerprint: "SHA256:abcd");

        var request = new CreateVmRequest(
            ClusterId: clusterId,
            Name: "vm-custom",
            Flavor: "small",
            Config: overlay);

        await sut.CreateAsync(request, CancellationToken.None);

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateVmCommand>(
                cmd => cmd.Config == overlay),
            Arg.Any<CancellationToken>());
    }
}
