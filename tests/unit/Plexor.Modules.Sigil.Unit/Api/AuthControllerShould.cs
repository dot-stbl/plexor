// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthControllerShould — verify AuthController dispatches the 4 auth
// endpoints (login / refresh / logout / me) to the right
// ICommandHandler<...> and returns the expected ActionResult. Uses
// NSubstitute per testing-unit.md §7 — no DbContext, no JwtSigningService.
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Plexor.Modules.Sigil.Api.Controllers;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Api;

public sealed class AuthControllerShould
{
    [Fact(DisplayName = "Given a login request, when LoginAsync is called, then dispatches LoginCommand and returns 200 Ok with LoginResult")]
    public async Task LoginAsyncDispatchesAndReturnsOkAsync()
    {
        var loginHandler = Substitute.For<ICommandHandler<LoginCommand, LoginResult>>();
        var refreshHandler = Substitute.For<ICommandHandler<RefreshCommand, LoginResult>>();
        var logoutHandler = Substitute.For<ICommandHandler<LogoutCommand, LogoutResult>>();
        var meHandler = Substitute.For<ICommandHandler<MeQuery, MeResult>>();
        var sut = new AuthController(loginHandler, refreshHandler, logoutHandler, meHandler);

        var orgId = Guid.NewGuid();
        var expected = new LoginResult(
            AccessToken: "access-token",
            RefreshToken: "refresh-token",
            AccessTokenExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15));
        loginHandler.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new LoginRequest(
            OrgId: orgId,
            Email: "user@example.com",
            Username: null,
            Password: "p4ssw0rd!");

        var actionResult = await sut.LoginAsync(request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await loginHandler.Received(1).HandleAsync(
            Arg.Is<LoginCommand>(c =>
                c.OrgId == orgId
                && c.Email == "user@example.com"
                && c.Username == null
                && c.Password == "p4ssw0rd!"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a refresh request, when RefreshAsync is called, then dispatches RefreshCommand and returns 200 Ok with new LoginResult")]
    public async Task RefreshAsyncDispatchesAndReturnsOkAsync()
    {
        var loginHandler = Substitute.For<ICommandHandler<LoginCommand, LoginResult>>();
        var refreshHandler = Substitute.For<ICommandHandler<RefreshCommand, LoginResult>>();
        var logoutHandler = Substitute.For<ICommandHandler<LogoutCommand, LogoutResult>>();
        var meHandler = Substitute.For<ICommandHandler<MeQuery, MeResult>>();
        var sut = new AuthController(loginHandler, refreshHandler, logoutHandler, meHandler);

        var expected = new LoginResult(
            AccessToken: "new-access-token",
            RefreshToken: "new-refresh-token",
            AccessTokenExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15));
        refreshHandler.HandleAsync(Arg.Any<RefreshCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new RefreshRequest(RefreshToken: "old-refresh-token");

        var actionResult = await sut.RefreshAsync(request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await refreshHandler.Received(1).HandleAsync(
            Arg.Is<RefreshCommand>(static c => c.RefreshToken == "old-refresh-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a logout request, when LogoutAsync is called, then dispatches LogoutCommand and returns 200 Ok with LogoutResult")]
    public async Task LogoutAsyncDispatchesAndReturnsOkAsync()
    {
        var loginHandler = Substitute.For<ICommandHandler<LoginCommand, LoginResult>>();
        var refreshHandler = Substitute.For<ICommandHandler<RefreshCommand, LoginResult>>();
        var logoutHandler = Substitute.For<ICommandHandler<LogoutCommand, LogoutResult>>();
        var meHandler = Substitute.For<ICommandHandler<MeQuery, MeResult>>();
        var sut = new AuthController(loginHandler, refreshHandler, logoutHandler, meHandler);

        var expected = new LogoutResult(RevokedTokens: 1);
        logoutHandler.HandleAsync(Arg.Any<LogoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new LogoutRequest(RefreshToken: "refresh-token");

        var actionResult = await sut.LogoutAsync(request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await logoutHandler.Received(1).HandleAsync(
            Arg.Is<LogoutCommand>(static c => c.RefreshToken == "refresh-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given no args, when MeAsync is called, then dispatches MeQuery and returns 200 Ok with MeResult")]
    public async Task MeAsyncDispatchesAndReturnsOkAsync()
    {
        var loginHandler = Substitute.For<ICommandHandler<LoginCommand, LoginResult>>();
        var refreshHandler = Substitute.For<ICommandHandler<RefreshCommand, LoginResult>>();
        var logoutHandler = Substitute.For<ICommandHandler<LogoutCommand, LogoutResult>>();
        var meHandler = Substitute.For<ICommandHandler<MeQuery, MeResult>>();
        var sut = new AuthController(loginHandler, refreshHandler, logoutHandler, meHandler);

        var expected = new MeResult(
            UserId: Guid.NewGuid(),
            OrgId: Guid.NewGuid(),
            Roles: ["admin"],
            Permissions: ["iam.users.read"]);
        meHandler.HandleAsync(Arg.Any<MeQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.MeAsync(CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await meHandler.Received(1).HandleAsync(Arg.Any<MeQuery>(), Arg.Any<CancellationToken>());
    }
}
