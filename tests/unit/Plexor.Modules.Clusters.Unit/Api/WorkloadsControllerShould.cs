// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadsControllerShould — verify WorkloadsController dispatches the
// 7 workload endpoints (4 CRUD + 3 Tier-5 actions) to the right
// ICommandHandler<...> and returns the expected ActionResult. Uses
// NSubstitute per testing-unit.md §7 — no DbContext.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Plexor.Modules.Clusters.Api.Controllers;
using Plexor.Modules.Clusters.Api.Models;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

// Disambiguate the Infrastructure.Clusters.Unit marker type from the
// namespace Plexor.Modules.Clusters.Unit (the test project's root
// namespace). The alias MUST be named differently — `using Unit = ...`
// is silently shadowed by the in-scope namespace Plexor.Modules.Clusters.Unit.
using UnitMarker = Plexor.Modules.Clusters.Infrastructure.Clusters.Unit;

namespace Plexor.Modules.Clusters.Unit.Api;

public sealed class WorkloadsControllerShould
{
    [Fact(DisplayName = "Given a create request, when CreateAsync is called, then dispatches CreateWorkloadCommand and returns 201 CreatedAtAction")]
    public async Task CreateAsyncDispatchesAndReturnsCreatedAtActionAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>>();
        var getHandler = Substitute.For<ICommandHandler<GetWorkloadQuery, WorkloadSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteWorkloadCommand, UnitMarker>>();
        var actionHandler = Substitute.For<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>>();
        var sut = new WorkloadsController(
            createHandler, listHandler, getHandler, deleteHandler, actionHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();
        var expected = new WorkloadSummary
        {
            Id = workloadId,
            ClusterId = clusterId,
            Name = "vm-alpha",
            Kind = "vm",
            State = WorkloadState.Provisioning,
        };
        createHandler.HandleAsync(Arg.Any<CreateWorkloadCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new CreateWorkloadRequest(
            Name: "vm-alpha",
            Kind: "vm",
            SpecJson: /*lang=json,strict*/ """{"image":"ubuntu-22.04"}""");

        var actionResult = await sut.CreateAsync(clusterId, request, CancellationToken.None);

        var created = actionResult.Result.ShouldBeOfType<CreatedAtActionResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Value.ShouldBe(expected);

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateWorkloadCommand>(
                command => command.ClusterId == clusterId
                           && command.Name == "vm-alpha"
                           && command.Kind == "vm"
                           && command.SpecJson == /*lang=json,strict*/ """{"image":"ubuntu-22.04"}"""),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a list query, when ListAsync is called, then dispatches ListWorkloadsQuery and returns 200 Ok with page")]
    public async Task ListAsyncDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>>();
        var getHandler = Substitute.For<ICommandHandler<GetWorkloadQuery, WorkloadSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteWorkloadCommand, UnitMarker>>();
        var actionHandler = Substitute.For<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>>();
        var sut = new WorkloadsController(
            createHandler, listHandler, getHandler, deleteHandler, actionHandler);

        var clusterId = IdGenerator.NewClusterId();
        var query = new FilterQuery { Page = 1, PageSize = 25 };
        var expected = new PageResult<WorkloadSummary>(
            Items: [],
            Total: 0,
            Page: 1,
            PageSize: 25);
        listHandler.HandleAsync(Arg.Any<ListWorkloadsQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.ListAsync(clusterId, query, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await listHandler.Received(1).HandleAsync(
            Arg.Is<ListWorkloadsQuery>(q => q.ClusterId == clusterId && q.Query.Page == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a workload id, when GetAsync is called, then dispatches GetWorkloadQuery and returns 200 Ok with summary")]
    public async Task GetAsyncDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>>();
        var getHandler = Substitute.For<ICommandHandler<GetWorkloadQuery, WorkloadSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteWorkloadCommand, UnitMarker>>();
        var actionHandler = Substitute.For<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>>();
        var sut = new WorkloadsController(
            createHandler, listHandler, getHandler, deleteHandler, actionHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();
        var expected = new WorkloadSummary { Id = workloadId, ClusterId = clusterId, Name = "vm-alpha" };
        getHandler.HandleAsync(Arg.Any<GetWorkloadQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.GetAsync(clusterId, workloadId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await getHandler.Received(1).HandleAsync(
            Arg.Is<GetWorkloadQuery>(q => q.ClusterId == clusterId && q.WorkloadId == workloadId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a workload id, when DeleteAsync is called, then dispatches DeleteWorkloadCommand and returns 204 NoContent")]
    public async Task DeleteAsyncDispatchesAndReturnsNoContentAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>>();
        var getHandler = Substitute.For<ICommandHandler<GetWorkloadQuery, WorkloadSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteWorkloadCommand, UnitMarker>>();
        var actionHandler = Substitute.For<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>>();
        var sut = new WorkloadsController(
            createHandler, listHandler, getHandler, deleteHandler, actionHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();

        var result = await sut.DeleteAsync(clusterId, workloadId, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();
        ((NoContentResult)result).StatusCode.ShouldBe(StatusCodes.Status204NoContent);

        await deleteHandler.Received(1).HandleAsync(
            Arg.Is<DeleteWorkloadCommand>(c => c.ClusterId == clusterId && c.WorkloadId == workloadId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a workload id, when StartAsync is called, then dispatches WorkloadActionCommand with Start and returns the handler's result")]
    public async Task StartAsyncDispatchesAndReturnsResultAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>>();
        var getHandler = Substitute.For<ICommandHandler<GetWorkloadQuery, WorkloadSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteWorkloadCommand, UnitMarker>>();
        var actionHandler = Substitute.For<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>>();
        var sut = new WorkloadsController(
            createHandler, listHandler, getHandler, deleteHandler, actionHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();
        var expected = new WorkloadActionResult(
            CommandId: Guid.NewGuid(),
            State: WorkloadState.Running);
        actionHandler.HandleAsync(Arg.Any<WorkloadActionCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await sut.StartAsync(clusterId, workloadId, CancellationToken.None);

        result.ShouldBe(expected);

        await actionHandler.Received(1).HandleAsync(
            Arg.Is<WorkloadActionCommand>(c =>
                c.ClusterId == clusterId
                && c.WorkloadId == workloadId
                && c.Action == WorkloadAction.Start),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a workload id, when StopAsync is called, then dispatches WorkloadActionCommand with Stop and returns the handler's result")]
    public async Task StopAsyncDispatchesAndReturnsResultAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>>();
        var getHandler = Substitute.For<ICommandHandler<GetWorkloadQuery, WorkloadSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteWorkloadCommand, UnitMarker>>();
        var actionHandler = Substitute.For<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>>();
        var sut = new WorkloadsController(
            createHandler, listHandler, getHandler, deleteHandler, actionHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();
        var expected = new WorkloadActionResult(
            CommandId: Guid.NewGuid(),
            State: WorkloadState.Stopped);
        actionHandler.HandleAsync(Arg.Any<WorkloadActionCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await sut.StopAsync(clusterId, workloadId, CancellationToken.None);

        result.ShouldBe(expected);

        await actionHandler.Received(1).HandleAsync(
            Arg.Is<WorkloadActionCommand>(c =>
                c.ClusterId == clusterId
                && c.WorkloadId == workloadId
                && c.Action == WorkloadAction.Stop),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a workload id, when RestartAsync is called, then dispatches WorkloadActionCommand with Restart and returns the handler's result")]
    public async Task RestartAsyncDispatchesAndReturnsResultAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateWorkloadCommand, WorkloadSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListWorkloadsQuery, PageResult<WorkloadSummary>>>();
        var getHandler = Substitute.For<ICommandHandler<GetWorkloadQuery, WorkloadSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteWorkloadCommand, UnitMarker>>();
        var actionHandler = Substitute.For<ICommandHandler<WorkloadActionCommand, WorkloadActionResult>>();
        var sut = new WorkloadsController(
            createHandler, listHandler, getHandler, deleteHandler, actionHandler);

        var clusterId = IdGenerator.NewClusterId();
        var workloadId = IdGenerator.NewWorkloadId();
        var expected = new WorkloadActionResult(
            CommandId: Guid.NewGuid(),
            State: WorkloadState.Running);
        actionHandler.HandleAsync(Arg.Any<WorkloadActionCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await sut.RestartAsync(clusterId, workloadId, CancellationToken.None);

        result.ShouldBe(expected);

        await actionHandler.Received(1).HandleAsync(
            Arg.Is<WorkloadActionCommand>(c =>
                c.ClusterId == clusterId
                && c.WorkloadId == workloadId
                && c.Action == WorkloadAction.Restart),
            Arg.Any<CancellationToken>());
    }
}
