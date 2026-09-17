// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateLoadBalancerEndpointShould — exercise the POST
// /api/v1/network/load-balancers handler in isolation. Mirrors the
// Plexor.Modules.Network.Unit.Endpoints.CreateFloatingIpEndpointShould
// pattern (real DbContext + NSubstitute ICurrentUser).
//
// Six tests pin the contract:
//   1. Happy path: valid Type + Algorithm + cluster id → 201 Created
//      with the projected body + the row lands in storage with the
//      right org id; the IQuotaEnforcer reserves
//      network.load_balancers.count (amount = 1) at the caller's
//      org scope.
//   2. Validation failure: malformed Type ("NotARealType") → 400
//      with a ValidationProblemDetails body; no quota reservation,
//      no INSERT.
//   3. Quota denied: IQuotaEnforcer returns Denied for
//      network.load_balancers.count → QuotaExceededException bubbles
//      out of the endpoint (the global QuotaExceptionHandler maps it
//      to 429 in production); no row lands in storage.
//   4. Cross-tenant read: GetAsync returns null when the (id, orgId)
//      predicate doesn't match → endpoint returns 404.
//   5. Cross-tenant delete: DeleteAsync returns false when the
//      (id, orgId) predicate doesn't match → endpoint returns 404.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Plexor.Modules.Network.Api.Endpoints;
using Plexor.Modules.Network.Api.Models.Requests;
using Plexor.Modules.Network.Api.Models.Responses;
using Plexor.Modules.Network.Api.Validation;
using Plexor.Modules.Network.Application.LoadBalancers;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Infrastructure.LoadBalancers;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Network.Unit.Endpoints;

/// <summary>
///     Behavioural tests for
///     <see cref="CreateLoadBalancerEndpoint.HandleAsync" /> + the
///     tenant scoping in <see cref="GetLoadBalancerEndpoint.HandleAsync" />
///     + <see cref="DeleteLoadBalancerEndpoint.HandleAsync" />. The
///     handler resolves <see cref="ILoadBalancerService" /> (scoped,
///     in-memory) + <see cref="ICurrentUser" /> (NSubstitute).
/// </summary>
public sealed class CreateLoadBalancerEndpointShould
{
    private static readonly Guid StubActorUserId = Guid.NewGuid();

    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    private static ServiceProvider BuildServices(NetworkDbContext db, IQuotaEnforcer enforcer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(enforcer);
        services.AddScoped<ILoadBalancerService, EfLoadBalancerService>(sp =>
            new EfLoadBalancerService(db, TimeProvider.System, enforcer, StubCurrentUser(Guid.NewGuid())));
        services.AddScoped<IValidator<CreateLoadBalancerRequest>, CreateLoadBalancerRequestValidator>();
        return services.BuildServiceProvider();
    }

    private static ICurrentUser StubCurrentUser(Guid tenantId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(StubActorUserId);
        currentUser.TenantId.Returns(tenantId);
        return currentUser;
    }

    /// <summary>NSubstitute-backed <see cref="IQuotaEnforcer" /> that
    /// always returns <see cref="QuotaCheckResult.Allowed" />. Lets
    /// the existing happy-path tests stay focused on the load
    /// balancer row path without spinning up the EF enforcer (which
    /// requires a real Postgres). Tests that need to exercise the
    /// denied path swap this for a customised substitute that returns
    /// <see cref="QuotaCheckResult.Denied" />.</summary>
    private static IQuotaEnforcer AllowedQuotaEnforcer()
    {
        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Allowed());
        return enforcer;
    }

    /// <summary>Happy path: valid Type + Algorithm + cluster id →
    /// 201 Created with the row persisted under the caller's tenant;
    /// the enforcer saw the network.load_balancers.count reservation
    /// at the caller's org scope with the actor user id from
    /// ICurrentUser.</summary>
    [Fact(DisplayName = "Given a valid CreateLoadBalancerRequest, when the handler runs, then the load balancer is created and a 201 Created response is returned")]
    public async Task HappyPath_CreatesLoadBalancerAndReturns201Async()
    {
        await using var db = NetworkTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var enforcer = AllowedQuotaEnforcer();
        var sp = BuildServices(db, enforcer);

        var request = new CreateLoadBalancerRequest
        {
            ClusterId = Guid.NewGuid(),
            Name = "edge-lb-001",
            Type = "L7",
            Algorithm = "RoundRobin",
        };

        var result = await CreateLoadBalancerEndpoint.HandleAsync(
            request,
            sp.GetRequiredService<ILoadBalancerService>(),
            currentUser,
            sp.GetRequiredService<IValidator<CreateLoadBalancerRequest>>(),
            CancellationToken.None);

        var created = result.ShouldBeOfType<Created<LoadBalancerSummary>>();
        created.Value.ShouldNotBeNull();
        created.Value!.Name.ShouldBe("edge-lb-001");
        created.Value.Type.ShouldBe("L7");
        created.Value.Algorithm.ShouldBe(nameof(LoadBalancerAlgorithm.RoundRobin));
        created.Value.Status.ShouldBe(nameof(NetworkResourceStatus.Pending));

        var stored = db.LoadBalancers.Single();
        stored.OrgId.ShouldBe(tenantId);
        stored.Name.ShouldBe("edge-lb-001");
        stored.Type.ShouldBe(LoadBalancerType.L7);
        stored.Algorithm.ShouldBe(LoadBalancerAlgorithm.RoundRobin);
        stored.Status.ShouldBe(NetworkResourceStatus.Pending);

        await enforcer.Received(1).CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope =>
                scope.Kind == QuotaScopeKind.Org &&
                scope.Id == tenantId &&
                scope.OrgId == tenantId &&
                scope.ActorUserId == StubActorUserId),
            QuotaDefinitionKey.LoadBalancersCount,
            1m,
            Arg.Any<CancellationToken>());
    }

    /// <summary>Validation failure: unknown Type → 400 BadRequest +
    /// the validator's dictionary; the enforcer is NOT invoked.</summary>
    [Fact(DisplayName = "Given an invalid Type, when the handler runs, then validation fails and the response is 400 BadRequest")]
    public async Task ValidationFailure_Returns400BadRequestAsync()
    {
        await using var db = NetworkTestDb.Create();
        var enforcer = AllowedQuotaEnforcer();
        var sp = BuildServices(db, enforcer);

        var request = new CreateLoadBalancerRequest
        {
            ClusterId = Guid.NewGuid(),
            Name = "bad-lb",
            Type = "NotARealType",
            Algorithm = "RoundRobin",
        };

        var result = await CreateLoadBalancerEndpoint.HandleAsync(
            request,
            sp.GetRequiredService<ILoadBalancerService>(),
            StubCurrentUser(Guid.NewGuid()),
            sp.GetRequiredService<IValidator<CreateLoadBalancerRequest>>(),
            CancellationToken.None);

        var bad = result.ShouldBeOfType<BadRequest<ValidationProblemDetails>>();
        bad.Value.ShouldNotBeNull();
        bad.Value!.Errors.ShouldContainKey(nameof(CreateLoadBalancerRequest.Type));
        db.LoadBalancers.ShouldBeEmpty();
        await enforcer.DidNotReceive().CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given the IQuotaEnforcer returns Denied for
    /// network.load_balancers.count, when the handler runs, then
    /// QuotaExceededException bubbles out of the endpoint (the
    /// global QuotaExceptionHandler maps it to 429 in production)
    /// and no row lands in storage.</summary>
    [Fact(DisplayName = "Given network.load_balancers.count at limit, when the handler runs, then the endpoint throws QuotaExceededException and no row is created")]
    public async Task QuotaDenied_ThrowsQuotaExceededExceptionAsync()
    {
        await using var db = NetworkTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope => scope.Id == tenantId),
            QuotaDefinitionKey.LoadBalancersCount,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Denied(
                Limit: 10m,
                Used: 10m,
                Requested: 1m,
                Reason: "network.load_balancers.count at limit"));
        var sp = BuildServices(db, enforcer);

        var request = new CreateLoadBalancerRequest
        {
            ClusterId = Guid.NewGuid(),
            Name = "over-quota-lb",
            Type = "L4",
            Algorithm = "RoundRobin",
        };

        var exception = await Should.ThrowAsync<QuotaExceededException>(async () =>
            await CreateLoadBalancerEndpoint.HandleAsync(
                request,
                sp.GetRequiredService<ILoadBalancerService>(),
                currentUser,
                sp.GetRequiredService<IValidator<CreateLoadBalancerRequest>>(),
                CancellationToken.None));

        exception.Limit.ShouldBe(10m);
        exception.Used.ShouldBe(10m);
        exception.Requested.ShouldBe(1m);
        exception.Code.ShouldBe(QuotaExceptions.Exceeded);

        db.LoadBalancers.ShouldBeEmpty();
    }

    /// <summary>Cross-tenant: GetAsync on a foreign load balancer
    /// returns null → 404 NotFound.</summary>
    [Fact(DisplayName = "Given a load balancer in another org, when the get-by-id endpoint runs for the caller's org, then the response is 404 NotFound")]
    public async Task CrossTenant_Get_Returns404NotFoundAsync()
    {
        await using var db = NetworkTestDb.Create();
        var ownerOrgId = Guid.NewGuid();
        var callerOrgId = Guid.NewGuid();
        var lb = new LoadBalancer
        {
            Id = Guid.NewGuid(),
            OrgId = ownerOrgId,
            ClusterId = Guid.NewGuid(),
            Name = "other-org-lb",
            Type = LoadBalancerType.L4,
            Algorithm = LoadBalancerAlgorithm.RoundRobin,
            Status = NetworkResourceStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.LoadBalancers.AddAsync(lb);
        await db.SaveChangesAsync();
        var sp = BuildServices(db, AllowedQuotaEnforcer());

        var result = await GetLoadBalancerEndpoint.HandleAsync(
            lb.Id,
            sp.GetRequiredService<ILoadBalancerService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
    }

    /// <summary>Cross-tenant: DeleteAsync on a foreign load
    /// balancer returns false → 404 NotFound + the row stays in the
    /// database.</summary>
    [Fact(DisplayName = "Given a load balancer in another org, when the delete endpoint runs for the caller's org, then the response is 404 and the row is not deleted")]
    public async Task CrossTenant_Delete_Returns404NotFoundAndKeepsRowAsync()
    {
        await using var db = NetworkTestDb.Create();
        var ownerOrgId = Guid.NewGuid();
        var callerOrgId = Guid.NewGuid();
        var lb = new LoadBalancer
        {
            Id = Guid.NewGuid(),
            OrgId = ownerOrgId,
            ClusterId = Guid.NewGuid(),
            Name = "other-org-lb",
            Type = LoadBalancerType.L4,
            Algorithm = LoadBalancerAlgorithm.RoundRobin,
            Status = NetworkResourceStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.LoadBalancers.AddAsync(lb);
        await db.SaveChangesAsync();
        var sp = BuildServices(db, AllowedQuotaEnforcer());

        var result = await DeleteLoadBalancerEndpoint.HandleAsync(
            lb.Id,
            sp.GetRequiredService<ILoadBalancerService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
        db.LoadBalancers.Single().Id.ShouldBe(lb.Id);
    }
}