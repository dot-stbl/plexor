// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClustersControllerShould — verify ClustersController dispatches the
// 7 cluster CRUD endpoints to the right ICommandHandler<...> and
// returns the expected ActionResult. Uses NSubstitute per
// testing-unit.md §7 — no DbContext.
//
// Note on POST /clusters: the controller currently returns Ok (200)
// with the JoinTokenResult. The issue brief mentioned "CreatedAtAction
// 201" — the controller does not implement that yet. The test
// therefore asserts the actual 200 Ok behaviour; switching to 201
// CreatedAtAction + Location header is a follow-up.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Plexor.Modules.Clusters.Api.Controllers;
using Plexor.Modules.Clusters.Api.Models;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering.Query;
using Plexor.Shared.Identifiers;
using Shouldly;
using Xunit;

// Disambiguate the Infrastructure.Clusters.Unit marker type from the
// namespace Plexor.Modules.Clusters.Unit (the test project's root
// namespace). Without the alias, the bare token `Unit` resolves to
// the namespace, not the marker class. The alias MUST be named
// differently — `using Unit = ...` is silently shadowed by the
// in-scope namespace `Plexor.Modules.Clusters.Unit`.
using UnitMarker = Plexor.Modules.Clusters.Infrastructure.Clusters.Unit;

namespace Plexor.Modules.Clusters.Unit.Api;

public sealed class ClustersControllerShould
{
    [Fact(DisplayName = "Given a create request, when CreateAsync is called, then dispatches CreateClusterCommand and returns 200 Ok with join token")]
    public async Task CreateAsyncDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateClusterCommand, JoinTokenResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateClusterCommand, ClusterSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteClusterCommand, UnitMarker>>();
        var getHandler = Substitute.For<ICommandHandler<GetClusterQuery, ClusterDetail>>();
        var listHandler = Substitute.For<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>>();
        var rotateHandler = Substitute.For<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>>();
        var sut = new ClustersController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler, rotateHandler);

        var clusterId = IdGenerator.NewClusterId();
        var expected = new JoinTokenResult(
            ClusterId: clusterId,
            Token: "join-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(7),
            Endpoint: "https://cp.example.com");
        createHandler.HandleAsync(Arg.Any<CreateClusterCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new CreateClusterRequest(
            Name: "prod-eu-1",
            Region: "eu-central-1",
            InitialNodeRole: NodeRole.Control);

        var actionResult = await sut.CreateAsync(request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateClusterCommand>(
                static command => command.OrgId == Guid.Empty
                           && command.Name == "prod-eu-1"
                           && command.Region == "eu-central-1"
                           && command.InitialNodeRole == NodeRole.Control),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a list query, when ListAsync is called, then dispatches ListClustersQuery and returns 200 Ok with page")]
    public async Task ListAsyncDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateClusterCommand, JoinTokenResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateClusterCommand, ClusterSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteClusterCommand, UnitMarker>>();
        var getHandler = Substitute.For<ICommandHandler<GetClusterQuery, ClusterDetail>>();
        var listHandler = Substitute.For<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>>();
        var rotateHandler = Substitute.For<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>>();

        var sut = new ClustersController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler, rotateHandler);

        var query = new FilterQuery { Page = 1, PageSize = 25 };
        var expected = new PageResult<ClusterSummary>(
            Items: [],
            Total: 0,
            Page: 1,
            PageSize: 25);
        listHandler.HandleAsync(Arg.Any<ListClustersQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.ListAsync(query, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await listHandler.Received(1).HandleAsync(
            Arg.Is<ListClustersQuery>(static q => q.OrgId == Guid.Empty && q.Query.Page == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a cluster id, when GetAsync is called, then dispatches GetClusterQuery and returns 200 Ok with cluster detail")]
    public async Task GetAsyncDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateClusterCommand, JoinTokenResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateClusterCommand, ClusterSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteClusterCommand, UnitMarker>>();
        var getHandler = Substitute.For<ICommandHandler<GetClusterQuery, ClusterDetail>>();
        var listHandler = Substitute.For<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>>();
        var rotateHandler = Substitute.For<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>>();

        var sut = new ClustersController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler, rotateHandler);

        var clusterId = IdGenerator.NewClusterId();
        var expected = new ClusterDetail { Id = clusterId, Name = "prod-eu-1" };
        getHandler.HandleAsync(Arg.Any<GetClusterQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.GetAsync(clusterId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await getHandler.Received(1).HandleAsync(
            Arg.Is<GetClusterQuery>(q => q.ClusterId == clusterId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given an update request, when UpdateAsync is called, then dispatches UpdateClusterCommand and returns 200 Ok with updated summary")]
    public async Task UpdateAsyncDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateClusterCommand, JoinTokenResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateClusterCommand, ClusterSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteClusterCommand, UnitMarker>>();
        var getHandler = Substitute.For<ICommandHandler<GetClusterQuery, ClusterDetail>>();
        var listHandler = Substitute.For<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>>();
        var rotateHandler = Substitute.For<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>>();

        var sut = new ClustersController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler, rotateHandler);

        var clusterId = IdGenerator.NewClusterId();
        var expected = new ClusterSummary { Id = clusterId, Name = "prod-eu-2" };
        updateHandler.HandleAsync(Arg.Any<UpdateClusterCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new UpdateClusterRequest(Name: "prod-eu-2", Region: "eu-west-1");

        var actionResult = await sut.UpdateAsync(clusterId, request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await updateHandler.Received(1).HandleAsync(
            Arg.Is<UpdateClusterCommand>(
                command => command.ClusterId == clusterId
                           && command.Name == "prod-eu-2"
                           && command.Region == "eu-west-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a cluster id, when DeleteAsync is called, then dispatches DeleteClusterCommand and returns 204 NoContent")]
    public async Task DeleteAsyncDispatchesAndReturnsNoContentAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateClusterCommand, JoinTokenResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateClusterCommand, ClusterSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteClusterCommand, UnitMarker>>();
        var getHandler = Substitute.For<ICommandHandler<GetClusterQuery, ClusterDetail>>();
        var listHandler = Substitute.For<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>>();
        var rotateHandler = Substitute.For<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>>();

        var sut = new ClustersController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler, rotateHandler);

        var clusterId = IdGenerator.NewClusterId();

        var result = await sut.DeleteAsync(clusterId, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();
        var noContent = (NoContentResult)result;
        noContent.StatusCode.ShouldBe(StatusCodes.Status204NoContent);

        await deleteHandler.Received(1).HandleAsync(
            Arg.Is<DeleteClusterCommand>(command => command.ClusterId == clusterId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a cluster id, when RotateJoinTokenAsync is called, then dispatches RotateJoinTokenCommand and returns 200 Ok with new token")]
    public async Task RotateJoinTokenAsyncDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateClusterCommand, JoinTokenResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateClusterCommand, ClusterSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteClusterCommand, UnitMarker>>();
        var getHandler = Substitute.For<ICommandHandler<GetClusterQuery, ClusterDetail>>();
        var listHandler = Substitute.For<ICommandHandler<ListClustersQuery, PageResult<ClusterSummary>>>();
        var rotateHandler = Substitute.For<ICommandHandler<RotateJoinTokenCommand, JoinTokenResult>>();

        var sut = new ClustersController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler, rotateHandler);

        var clusterId = IdGenerator.NewClusterId();
        var expected = new JoinTokenResult(
            ClusterId: clusterId,
            Token: "rotated-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(7),
            Endpoint: "https://cp.example.com");
        rotateHandler.HandleAsync(Arg.Any<RotateJoinTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.RotateJoinTokenAsync(clusterId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await rotateHandler.Received(1).HandleAsync(
            Arg.Is<RotateJoinTokenCommand>(command => command.ClusterId == clusterId),
            Arg.Any<CancellationToken>());
    }

    // ListNodes moved to Outpost module in #55; the equivalent
    // endpoint test lives in tests/unit/Plexor.Modules.Outpost.Unit/Api/NodesControllerShould.cs.
}
