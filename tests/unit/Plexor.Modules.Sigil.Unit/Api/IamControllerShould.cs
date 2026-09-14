// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IamControllerShould — verify the 5 IAM controllers (IamController for
// users, IamRolesController, IamBindingsController, IamApiKeysController,
// IamSshKeysController) dispatch their endpoints to the right
// ICommandHandler<...> and return the expected ActionResult. Uses
// NSubstitute per testing-unit.md §7 — no DbContext, no JwtSigningService.
// ============================================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Plexor.Modules.Sigil.Api.Controllers;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Users;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Api;

/// <summary>
///     Verifies all 5 Sigil IAM controllers dispatch correctly. One test
///     file covers the user / role / role-binding / api-key / ssh-key
///     surfaces — they're small enough that splitting them out adds
///     ceremony without benefit.
/// </summary>
public sealed class IamControllerShould
{
    // ---------------------------------------------------------------------
    // IamController — user CRUD
    // ---------------------------------------------------------------------

    [Fact(DisplayName = "Given a create user request, when CreateAsync is called, then dispatches CreateUserCommand and returns 201 CreatedAtAction")]
    public async Task CreateUserDispatchesAndReturnsCreatedAtActionAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateUserCommand, CreateUserResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateUserCommand, UserSummary>>();
        var disableHandler = Substitute.For<ICommandHandler<DisableUserCommand, UserSummary>>();
        var changePasswordHandler = Substitute.For<ICommandHandler<ChangePasswordCommand, ChangePasswordResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetUserQuery, UserSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListUsersQuery, UserPage>>();
        var sut = new IamController(
            createHandler, updateHandler, disableHandler, changePasswordHandler, getHandler, listHandler);

        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var expected = new CreateUserResult(UserId: userId);
        createHandler.HandleAsync(Arg.Any<CreateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new CreateUserRequest(
            OrgId: orgId,
            Email: "user@example.com",
            DisplayName: "User One",
            Password: "p4ssw0rd!");

        var actionResult = await sut.CreateAsync(request, CancellationToken.None);

        var created = actionResult.Result.ShouldBeOfType<CreatedAtActionResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Value.ShouldBe(expected);

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateUserCommand>(c =>
                c.OrgId == orgId
                && c.Email == "user@example.com"
                && c.DisplayName == "User One"
                && c.Password == "p4ssw0rd!"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a user id, when GetAsync is called, then dispatches GetUserQuery and returns 200 Ok with summary")]
    public async Task GetUserDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateUserCommand, CreateUserResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateUserCommand, UserSummary>>();
        var disableHandler = Substitute.For<ICommandHandler<DisableUserCommand, UserSummary>>();
        var changePasswordHandler = Substitute.For<ICommandHandler<ChangePasswordCommand, ChangePasswordResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetUserQuery, UserSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListUsersQuery, UserPage>>();
        var sut = new IamController(
            createHandler, updateHandler, disableHandler, changePasswordHandler, getHandler, listHandler);

        var userId = Guid.NewGuid();
        var expected = new UserSummary { Id = userId, Email = "user@example.com", DisplayName = "User One" };
        getHandler.HandleAsync(Arg.Any<GetUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.GetAsync(userId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await getHandler.Received(1).HandleAsync(
            Arg.Is<GetUserQuery>(q => q.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a list query, when ListAsync is called, then dispatches ListUsersQuery and returns 200 Ok with page")]
    public async Task ListUsersDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateUserCommand, CreateUserResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateUserCommand, UserSummary>>();
        var disableHandler = Substitute.For<ICommandHandler<DisableUserCommand, UserSummary>>();
        var changePasswordHandler = Substitute.For<ICommandHandler<ChangePasswordCommand, ChangePasswordResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetUserQuery, UserSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListUsersQuery, UserPage>>();
        var sut = new IamController(
            createHandler, updateHandler, disableHandler, changePasswordHandler, getHandler, listHandler);

        var orgId = Guid.NewGuid();
        var expected = new UserPage(Items: [], Total: 0, Page: 1, PageSize: 50);
        listHandler.HandleAsync(Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.ListAsync(orgId, 1, 50, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await listHandler.Received(1).HandleAsync(
            Arg.Is<ListUsersQuery>(q => q.OrgId == orgId && q.Page == 1 && q.PageSize == 50),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given an update request, when UpdateAsync is called, then dispatches UpdateUserCommand and returns 200 Ok with summary")]
    public async Task UpdateUserDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateUserCommand, CreateUserResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateUserCommand, UserSummary>>();
        var disableHandler = Substitute.For<ICommandHandler<DisableUserCommand, UserSummary>>();
        var changePasswordHandler = Substitute.For<ICommandHandler<ChangePasswordCommand, ChangePasswordResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetUserQuery, UserSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListUsersQuery, UserPage>>();
        var sut = new IamController(
            createHandler, updateHandler, disableHandler, changePasswordHandler, getHandler, listHandler);

        var userId = Guid.NewGuid();
        var expected = new UserSummary { Id = userId, Email = "user@example.com", DisplayName = "Renamed" };
        updateHandler.HandleAsync(Arg.Any<UpdateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new UpdateUserRequest(DisplayName: "Renamed", Status: null);

        var actionResult = await sut.UpdateAsync(userId, request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await updateHandler.Received(1).HandleAsync(
            Arg.Is<UpdateUserCommand>(c =>
                c.UserId == userId
                && c.DisplayName == "Renamed"
                && c.Status == null),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a user id, when DisableAsync is called, then dispatches DisableUserCommand and returns 200 Ok with summary")]
    public async Task DisableUserDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateUserCommand, CreateUserResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateUserCommand, UserSummary>>();
        var disableHandler = Substitute.For<ICommandHandler<DisableUserCommand, UserSummary>>();
        var changePasswordHandler = Substitute.For<ICommandHandler<ChangePasswordCommand, ChangePasswordResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetUserQuery, UserSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListUsersQuery, UserPage>>();
        var sut = new IamController(
            createHandler, updateHandler, disableHandler, changePasswordHandler, getHandler, listHandler);

        var userId = Guid.NewGuid();
        var expected = new UserSummary { Id = userId, Status = "suspended" };
        disableHandler.HandleAsync(Arg.Any<DisableUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.DisableAsync(userId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await disableHandler.Received(1).HandleAsync(
            Arg.Is<DisableUserCommand>(c => c.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a change-password request, when ChangePasswordAsync is called, then dispatches ChangePasswordCommand and returns 200 Ok with result")]
    public async Task ChangePasswordDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateUserCommand, CreateUserResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateUserCommand, UserSummary>>();
        var disableHandler = Substitute.For<ICommandHandler<DisableUserCommand, UserSummary>>();
        var changePasswordHandler = Substitute.For<ICommandHandler<ChangePasswordCommand, ChangePasswordResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetUserQuery, UserSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListUsersQuery, UserPage>>();
        var sut = new IamController(
            createHandler, updateHandler, disableHandler, changePasswordHandler, getHandler, listHandler);

        var userId = Guid.NewGuid();
        var expected = new ChangePasswordResult(UserId: userId, RefreshTokensRevoked: 1);
        changePasswordHandler.HandleAsync(Arg.Any<ChangePasswordCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new ChangePasswordRequest(
            CurrentPassword: "old-password",
            NewPassword: "new-password-1");

        var actionResult = await sut.ChangePasswordAsync(userId, request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await changePasswordHandler.Received(1).HandleAsync(
            Arg.Is<ChangePasswordCommand>(c =>
                c.UserId == userId
                && c.CurrentPassword == "old-password"
                && c.NewPassword == "new-password-1"),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------
    // IamRolesController — role CRUD
    // ---------------------------------------------------------------------

    [Fact(DisplayName = "Given a create role request, when IamRolesController.CreateAsync is called, then dispatches CreateRoleCommand and returns 201 CreatedAtAction")]
    public async Task CreateRoleDispatchesAndReturnsCreatedAtActionAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleCommand, CreateRoleResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateRoleCommand, RoleSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleCommand, DeleteRoleResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetRoleQuery, RoleSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>>>();
        var sut = new IamRolesController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler);

        var roleId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var expected = new CreateRoleResult(RoleId: roleId);
        createHandler.HandleAsync(Arg.Any<CreateRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new CreateRoleRequest(
            OrgId: orgId,
            Name: "custom-role",
            Description: "Test role",
            Permissions: ["iam.users.read"]);

        var actionResult = await sut.CreateAsync(request, CancellationToken.None);

        var created = actionResult.Result.ShouldBeOfType<CreatedAtActionResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Value.ShouldBe(expected);

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateRoleCommand>(c =>
                c.OrgId == orgId
                && c.Name == "custom-role"
                && c.Description == "Test role"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a role id, when IamRolesController.GetAsync is called, then dispatches GetRoleQuery and returns 200 Ok")]
    public async Task GetRoleDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleCommand, CreateRoleResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateRoleCommand, RoleSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleCommand, DeleteRoleResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetRoleQuery, RoleSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>>>();
        var sut = new IamRolesController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler);

        var roleId = Guid.NewGuid();
        var expected = new RoleSummary { Id = roleId, Name = "admin" };
        getHandler.HandleAsync(Arg.Any<GetRoleQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.GetAsync(roleId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await getHandler.Received(1).HandleAsync(
            Arg.Is<GetRoleQuery>(q => q.RoleId == roleId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given an org id, when IamRolesController.ListAsync is called, then dispatches ListRolesQuery and returns 200 Ok")]
    public async Task ListRolesDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleCommand, CreateRoleResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateRoleCommand, RoleSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleCommand, DeleteRoleResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetRoleQuery, RoleSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>>>();
        var sut = new IamRolesController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler);

        var orgId = Guid.NewGuid();
        IReadOnlyCollection<RoleSummary> expected = [];
        listHandler.HandleAsync(Arg.Any<ListRolesQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.ListAsync(orgId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await listHandler.Received(1).HandleAsync(
            Arg.Is<ListRolesQuery>(q => q.OrgId == orgId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given an update request, when IamRolesController.UpdateAsync is called, then dispatches UpdateRoleCommand and returns 200 Ok")]
    public async Task UpdateRoleDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleCommand, CreateRoleResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateRoleCommand, RoleSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleCommand, DeleteRoleResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetRoleQuery, RoleSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>>>();
        var sut = new IamRolesController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler);

        var roleId = Guid.NewGuid();
        var expected = new RoleSummary { Id = roleId, Name = "renamed" };
        updateHandler.HandleAsync(Arg.Any<UpdateRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new UpdateRoleRequest(Description: "Updated description", Permissions: null);

        var actionResult = await sut.UpdateAsync(roleId, request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await updateHandler.Received(1).HandleAsync(
            Arg.Is<UpdateRoleCommand>(c =>
                c.RoleId == roleId
                && c.Description == "Updated description"
                && c.Permissions == null),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a role id, when IamRolesController.DeleteAsync is called, then dispatches DeleteRoleCommand and returns 200 Ok")]
    public async Task DeleteRoleDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleCommand, CreateRoleResult>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateRoleCommand, RoleSummary>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleCommand, DeleteRoleResult>>();
        var getHandler = Substitute.For<ICommandHandler<GetRoleQuery, RoleSummary>>();
        var listHandler = Substitute.For<ICommandHandler<ListRolesQuery, IReadOnlyCollection<RoleSummary>>>();
        var sut = new IamRolesController(
            createHandler, updateHandler, deleteHandler, getHandler, listHandler);

        var roleId = Guid.NewGuid();
        var expected = new DeleteRoleResult(RoleId: roleId);
        deleteHandler.HandleAsync(Arg.Any<DeleteRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.DeleteAsync(roleId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await deleteHandler.Received(1).HandleAsync(
            Arg.Is<DeleteRoleCommand>(c => c.RoleId == roleId),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------
    // IamBindingsController — role bindings
    // ---------------------------------------------------------------------

    [Fact(DisplayName = "Given a create binding request, when IamBindingsController.CreateAsync is called, then dispatches CreateRoleBindingCommand and returns 201 Created")]
    public async Task CreateRoleBindingDispatchesAndReturnsCreatedAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleBindingCommand, CreateRoleBindingResult>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleBindingCommand, DeleteRoleBindingResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListRoleBindingsQuery, IReadOnlyCollection<RoleBindingSummary>>>();
        var sut = new IamBindingsController(createHandler, deleteHandler, listHandler);

        var bindingId = Guid.NewGuid();
        var expected = new CreateRoleBindingResult(BindingId: bindingId);
        createHandler.HandleAsync(Arg.Any<CreateRoleBindingCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new CreateRoleBindingRequest(
            OrgId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RoleId: Guid.NewGuid());

        var actionResult = await sut.CreateAsync(request, CancellationToken.None);

        var created = actionResult.Result.ShouldBeOfType<CreatedResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Value.ShouldBe(expected);

        await createHandler.Received(1).HandleAsync(
            Arg.Is<CreateRoleBindingCommand>(c =>
                c.OrgId == request.OrgId
                && c.UserId == request.UserId
                && c.RoleId == request.RoleId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a user id, when IamBindingsController.ListAsync is called, then dispatches ListRoleBindingsQuery and returns 200 Ok")]
    public async Task ListRoleBindingsDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleBindingCommand, CreateRoleBindingResult>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleBindingCommand, DeleteRoleBindingResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListRoleBindingsQuery, IReadOnlyCollection<RoleBindingSummary>>>();
        var sut = new IamBindingsController(createHandler, deleteHandler, listHandler);

        var userId = Guid.NewGuid();
        IReadOnlyCollection<RoleBindingSummary> expected = [];
        listHandler.HandleAsync(Arg.Any<ListRoleBindingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.ListAsync(userId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await listHandler.Received(1).HandleAsync(
            Arg.Is<ListRoleBindingsQuery>(q => q.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a binding id, when IamBindingsController.DeleteAsync is called, then dispatches DeleteRoleBindingCommand and returns 200 Ok")]
    public async Task DeleteRoleBindingDispatchesAndReturnsOkAsync()
    {
        var createHandler = Substitute.For<ICommandHandler<CreateRoleBindingCommand, CreateRoleBindingResult>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteRoleBindingCommand, DeleteRoleBindingResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListRoleBindingsQuery, IReadOnlyCollection<RoleBindingSummary>>>();
        var sut = new IamBindingsController(createHandler, deleteHandler, listHandler);

        var bindingId = Guid.NewGuid();
        var expected = new DeleteRoleBindingResult(BindingId: bindingId);
        deleteHandler.HandleAsync(Arg.Any<DeleteRoleBindingCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.DeleteAsync(bindingId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await deleteHandler.Received(1).HandleAsync(
            Arg.Is<DeleteRoleBindingCommand>(c => c.BindingId == bindingId),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------
    // IamApiKeysController — API keys
    // ---------------------------------------------------------------------

    [Fact(DisplayName = "Given an issue request, when IamApiKeysController.IssueAsync is called, then dispatches IssueApiKeyCommand and returns 200 Ok")]
    public async Task IssueApiKeyDispatchesAndReturnsOkAsync()
    {
        var issueHandler = Substitute.For<ICommandHandler<IssueApiKeyCommand, IssueApiKeyResult>>();
        var revokeHandler = Substitute.For<ICommandHandler<RevokeApiKeyCommand, RevokeApiKeyResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListApiKeysQuery, IReadOnlyCollection<ApiKeySummary>>>();
        var sut = new IamApiKeysController(issueHandler, revokeHandler, listHandler);

        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var expected = new IssueApiKeyResult(KeyId: Guid.NewGuid(), RawSecret: "raw-secret");
        issueHandler.HandleAsync(Arg.Any<IssueApiKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new IssueApiKeyRequest(
            OrgId: orgId,
            Name: "ci-bot",
            Permissions: ["iam.users.read"],
            ExpiresAtUtc: null);

        var actionResult = await sut.IssueAsync(userId, request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await issueHandler.Received(1).HandleAsync(
            Arg.Is<IssueApiKeyCommand>(c =>
                c.OwnerId == userId
                && c.OrgId == orgId
                && c.Name == "ci-bot"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a user id, when IamApiKeysController.ListAsync is called, then dispatches ListApiKeysQuery and returns 200 Ok")]
    public async Task ListApiKeysDispatchesAndReturnsOkAsync()
    {
        var issueHandler = Substitute.For<ICommandHandler<IssueApiKeyCommand, IssueApiKeyResult>>();
        var revokeHandler = Substitute.For<ICommandHandler<RevokeApiKeyCommand, RevokeApiKeyResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListApiKeysQuery, IReadOnlyCollection<ApiKeySummary>>>();
        var sut = new IamApiKeysController(issueHandler, revokeHandler, listHandler);

        var userId = Guid.NewGuid();
        IReadOnlyCollection<ApiKeySummary> expected = [];
        listHandler.HandleAsync(Arg.Any<ListApiKeysQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.ListAsync(userId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await listHandler.Received(1).HandleAsync(
            Arg.Is<ListApiKeysQuery>(q => q.OwnerId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a key id, when IamApiKeysController.RevokeAsync is called, then dispatches RevokeApiKeyCommand and returns 200 Ok")]
    public async Task RevokeApiKeyDispatchesAndReturnsOkAsync()
    {
        var issueHandler = Substitute.For<ICommandHandler<IssueApiKeyCommand, IssueApiKeyResult>>();
        var revokeHandler = Substitute.For<ICommandHandler<RevokeApiKeyCommand, RevokeApiKeyResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListApiKeysQuery, IReadOnlyCollection<ApiKeySummary>>>();
        var sut = new IamApiKeysController(issueHandler, revokeHandler, listHandler);

        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var expected = new RevokeApiKeyResult(KeyId: keyId);
        revokeHandler.HandleAsync(Arg.Any<RevokeApiKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.RevokeAsync(userId, keyId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await revokeHandler.Received(1).HandleAsync(
            Arg.Is<RevokeApiKeyCommand>(c => c.OwnerId == userId && c.KeyId == keyId),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------
    // IamSshKeysController — SSH keys
    // ---------------------------------------------------------------------

    [Fact(DisplayName = "Given an add request, when IamSshKeysController.AddAsync is called, then dispatches AddSshKeyCommand and returns 200 Ok")]
    public async Task AddSshKeyDispatchesAndReturnsOkAsync()
    {
        var addHandler = Substitute.For<ICommandHandler<AddSshKeyCommand, SshKeySummary>>();
        var revokeHandler = Substitute.For<ICommandHandler<RevokeSshKeyCommand, RevokeSshKeyResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListSshKeysQuery, IReadOnlyCollection<SshKeySummary>>>();
        var sut = new IamSshKeysController(addHandler, revokeHandler, listHandler);

        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var expected = new SshKeySummary { Id = Guid.NewGuid(), Fingerprint = "SHA256:abc123" };
        addHandler.HandleAsync(Arg.Any<AddSshKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var request = new AddSshKeyRequest(
            OrgId: orgId,
            Name: "work-laptop",
            PublicKey: "ssh-rsa AAAAB3Nza... user@host");

        var actionResult = await sut.AddAsync(userId, request, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await addHandler.Received(1).HandleAsync(
            Arg.Is<AddSshKeyCommand>(c =>
                c.OwnerId == userId
                && c.OrgId == orgId
                && c.Name == "work-laptop"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a user id, when IamSshKeysController.ListAsync is called, then dispatches ListSshKeysQuery and returns 200 Ok")]
    public async Task ListSshKeysDispatchesAndReturnsOkAsync()
    {
        var addHandler = Substitute.For<ICommandHandler<AddSshKeyCommand, SshKeySummary>>();
        var revokeHandler = Substitute.For<ICommandHandler<RevokeSshKeyCommand, RevokeSshKeyResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListSshKeysQuery, IReadOnlyCollection<SshKeySummary>>>();
        var sut = new IamSshKeysController(addHandler, revokeHandler, listHandler);

        var userId = Guid.NewGuid();
        IReadOnlyCollection<SshKeySummary> expected = [];
        listHandler.HandleAsync(Arg.Any<ListSshKeysQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.ListAsync(userId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await listHandler.Received(1).HandleAsync(
            Arg.Is<ListSshKeysQuery>(q => q.OwnerId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a key id, when IamSshKeysController.RevokeAsync is called, then dispatches RevokeSshKeyCommand and returns 200 Ok")]
    public async Task RevokeSshKeyDispatchesAndReturnsOkAsync()
    {
        var addHandler = Substitute.For<ICommandHandler<AddSshKeyCommand, SshKeySummary>>();
        var revokeHandler = Substitute.For<ICommandHandler<RevokeSshKeyCommand, RevokeSshKeyResult>>();
        var listHandler = Substitute.For<ICommandHandler<ListSshKeysQuery, IReadOnlyCollection<SshKeySummary>>>();
        var sut = new IamSshKeysController(addHandler, revokeHandler, listHandler);

        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var expected = new RevokeSshKeyResult(KeyId: keyId);
        revokeHandler.HandleAsync(Arg.Any<RevokeSshKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await sut.RevokeAsync(userId, keyId, CancellationToken.None);

        actionResult.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);

        await revokeHandler.Received(1).HandleAsync(
            Arg.Is<RevokeSshKeyCommand>(c => c.OwnerId == userId && c.KeyId == keyId),
            Arg.Any<CancellationToken>());
    }
}
