// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateFloatingIpEndpointShould — exercise the POST
// /api/v1/network/floating-ips handler in isolation. Mirrors the
// Plexor.Modules.Storage.Unit.Endpoints.CreateVolumeEndpointShould
// pattern (real DbContext + NSubstitute ICurrentUser).
//
// Six tests pin the contract:
//   1. Happy path: valid IP + cluster id → 201 Created with the
//      projected body + the row lands in storage with the right
//      org id; the IQuotaEnforcer reserves network.floating_ips.count
//      (amount = 1) at the caller's org scope.
//   2. IPv6 happy path: 2001:db8::1 → 201 Created.
//   3. Validation failure: malformed IP → 400 with a
//      ValidationProblemDetails body; no quota reservation, no INSERT.
//   4. Quota denied: IQuotaEnforcer returns Denied for
//      network.floating_ips.count → QuotaExceededException bubbles
//      out of the endpoint (the global QuotaExceptionHandler maps it
//      to 429 in production); no row lands in storage.
//   5. Cross-tenant read: GetAsync returns null when the (id, orgId)
//      predicate doesn't match → endpoint returns 404.
//   6. Cross-tenant delete: DeleteAsync returns false when the
//      (id, orgId) predicate doesn't match → endpoint returns 404.
// ============================================================================

using System.Net;
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
using Plexor.Modules.Network.Application.FloatingIps;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Infrastructure.FloatingIps;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Network.Unit.Endpoints;

/// <summary>
///     Behavioural tests for
///     <see cref="CreateFloatingIpEndpoint.HandleAsync" /> + the
///     tenant scoping in <see cref="GetFloatingIpEndpoint.HandleAsync" />
///     + <see cref="DeleteFloatingIpEndpoint.HandleAsync" />. The
///     handler resolves <see cref="IFloatingIpService" /> (scoped,
///     in-memory) + <see cref="ICurrentUser" /> (NSubstitute).
/// </summary>
public sealed class CreateFloatingIpEndpointShould
{
    private static readonly Guid StubActorUserId = Guid.NewGuid();

    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    private static ServiceProvider BuildServices(NetworkDbContext db, IQuotaEnforcer enforcer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(enforcer);
        services.AddScoped<IFloatingIpService, EfFloatingIpService>(sp =>
            new EfFloatingIpService(db, TimeProvider.System, enforcer, StubCurrentUser(Guid.NewGuid())));
        services.AddScoped<IValidator<CreateFloatingIpRequest>, CreateFloatingIpRequestValidator>();
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
    /// the existing happy-path tests stay focused on the floating IP
    /// row path without spinning up the EF enforcer (which requires
    /// a real Postgres). Tests that need to exercise the denied path
    /// swap this for a customised substitute that returns
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

    /// <summary>Happy path: valid IPv4 → 201 Created with the row
    /// persisted under the caller's tenant; the enforcer saw the
    /// network.floating_ips.count reservation at the caller's org
    /// scope with the actor user id from ICurrentUser.</summary>
    [Fact(DisplayName = "Given a valid CreateFloatingIpRequest, when the handler runs, then the floating IP is created and a 201 Created response is returned")]
    public async Task HappyPath_CreatesFloatingIpAndReturns201Async()
    {
        await using var db = NetworkTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var enforcer = AllowedQuotaEnforcer();
        var sp = BuildServices(db, enforcer);

        var request = new CreateFloatingIpRequest
        {
            ClusterId = Guid.NewGuid(),
            Address = "203.0.113.42",
        };

        var result = await CreateFloatingIpEndpoint.HandleAsync(
            request,
            sp.GetRequiredService<IFloatingIpService>(),
            currentUser,
            sp.GetRequiredService<IValidator<CreateFloatingIpRequest>>(),
            CancellationToken.None);

        var created = result.ShouldBeOfType<Created<FloatingIpDetail>>();
        created.Value.ShouldNotBeNull();
        created.Value!.Address.ShouldBe("203.0.113.42");
        created.Value.OrgId.ShouldBe(tenantId);
        created.Value.Status.ShouldBe(nameof(NetworkResourceStatus.Pending));

        var stored = db.FloatingIps.Single();
        stored.OrgId.ShouldBe(tenantId);
        stored.Address.ShouldBe("203.0.113.42");
        stored.Status.ShouldBe(NetworkResourceStatus.Pending);

        await enforcer.Received(1).CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope =>
                scope.Kind == QuotaScopeKind.Org &&
                scope.Id == tenantId &&
                scope.OrgId == tenantId &&
                scope.ActorUserId == StubActorUserId),
            QuotaDefinitionKey.FloatingIpsCount,
            1m,
            Arg.Any<CancellationToken>());
    }

    /// <summary>IPv6 happy path — the validator must accept v6
    /// addresses too.</summary>
    [Fact(DisplayName = "Given a valid IPv6 address, when the handler runs, then the IP is created and persisted")]
    public async Task HappyPath_AcceptsIpv6AddressAsync()
    {
        await using var db = NetworkTestDb.Create();
        var sp = BuildServices(db, AllowedQuotaEnforcer());

        var request = new CreateFloatingIpRequest
        {
            ClusterId = Guid.NewGuid(),
            Address = "2001:db8::1",
        };

        var result = await CreateFloatingIpEndpoint.HandleAsync(
            request,
            sp.GetRequiredService<IFloatingIpService>(),
            StubCurrentUser(Guid.NewGuid()),
            sp.GetRequiredService<IValidator<CreateFloatingIpRequest>>(),
            CancellationToken.None);

        result.ShouldBeOfType<Created<FloatingIpDetail>>();
    }

    /// <summary>Validation failure: malformed address → 400 BadRequest
    /// + the validator's dictionary; the enforcer is NOT invoked.</summary>
    [Fact(DisplayName = "Given an invalid IP address, when the handler runs, then validation fails and the response is 400 BadRequest")]
    public async Task ValidationFailure_Returns400BadRequestAsync()
    {
        await using var db = NetworkTestDb.Create();
        var enforcer = AllowedQuotaEnforcer();
        var sp = BuildServices(db, enforcer);

        var request = new CreateFloatingIpRequest
        {
            ClusterId = Guid.NewGuid(),
            Address = "not-an-ip",
        };

        var result = await CreateFloatingIpEndpoint.HandleAsync(
            request,
            sp.GetRequiredService<IFloatingIpService>(),
            StubCurrentUser(Guid.NewGuid()),
            sp.GetRequiredService<IValidator<CreateFloatingIpRequest>>(),
            CancellationToken.None);

        var bad = result.ShouldBeOfType<BadRequest<ValidationProblemDetails>>();
        bad.Value.ShouldNotBeNull();
        bad.Value!.Errors.ShouldContainKey(nameof(CreateFloatingIpRequest.Address));
        db.FloatingIps.ShouldBeEmpty();
        await enforcer.DidNotReceive().CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given the IQuotaEnforcer returns Denied for
    /// network.floating_ips.count, when the handler runs, then
    /// QuotaExceededException bubbles out of the endpoint (the
    /// global QuotaExceptionHandler maps it to 429 in production)
    /// and no row lands in storage.</summary>
    [Fact(DisplayName = "Given network.floating_ips.count at limit, when the handler runs, then the endpoint throws QuotaExceededException and no row is created")]
    public async Task QuotaDenied_ThrowsQuotaExceededExceptionAsync()
    {
        await using var db = NetworkTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope => scope.Id == tenantId),
            QuotaDefinitionKey.FloatingIpsCount,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Denied(
                Limit: 50m,
                Used: 50m,
                Requested: 1m,
                Reason: "network.floating_ips.count at limit"));
        var sp = BuildServices(db, enforcer);

        var request = new CreateFloatingIpRequest
        {
            ClusterId = Guid.NewGuid(),
            Address = "203.0.113.42",
        };

        var exception = await Should.ThrowAsync<QuotaExceededException>(async () =>
            await CreateFloatingIpEndpoint.HandleAsync(
                request,
                sp.GetRequiredService<IFloatingIpService>(),
                currentUser,
                sp.GetRequiredService<IValidator<CreateFloatingIpRequest>>(),
                CancellationToken.None));

        exception.Limit.ShouldBe(50m);
        exception.Used.ShouldBe(50m);
        exception.Requested.ShouldBe(1m);
        exception.Code.ShouldBe(QuotaExceptions.Exceeded);

        db.FloatingIps.ShouldBeEmpty();
    }

    /// <summary>Cross-tenant: GetAsync on a foreign IP returns null
    /// → 404 NotFound.</summary>
    [Fact(DisplayName = "Given a floating IP in another org, when the get-by-id endpoint runs for the caller's org, then the response is 404 NotFound")]
    public async Task CrossTenant_Get_Returns404NotFoundAsync()
    {
        await using var db = NetworkTestDb.Create();
        var ownerOrgId = Guid.NewGuid();
        var callerOrgId = Guid.NewGuid();
        var ip = new FloatingIp
        {
            Id = Guid.NewGuid(),
            OrgId = ownerOrgId,
            ClusterId = Guid.NewGuid(),
            Address = "203.0.113.1",
            Status = NetworkResourceStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.FloatingIps.AddAsync(ip);
        await db.SaveChangesAsync();
        var sp = BuildServices(db, AllowedQuotaEnforcer());

        var result = await GetFloatingIpEndpoint.HandleAsync(
            ip.Id,
            sp.GetRequiredService<IFloatingIpService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
    }

    /// <summary>Cross-tenant: DeleteAsync on a foreign IP returns
    /// false → 404 NotFound + the row stays in the database.</summary>
    [Fact(DisplayName = "Given a floating IP in another org, when the delete endpoint runs for the caller's org, then the response is 404 and the row is not deleted")]
    public async Task CrossTenant_Delete_Returns404NotFoundAndKeepsRowAsync()
    {
        await using var db = NetworkTestDb.Create();
        var ownerOrgId = Guid.NewGuid();
        var callerOrgId = Guid.NewGuid();
        var ip = new FloatingIp
        {
            Id = Guid.NewGuid(),
            OrgId = ownerOrgId,
            ClusterId = Guid.NewGuid(),
            Address = "203.0.113.99",
            Status = NetworkResourceStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.FloatingIps.AddAsync(ip);
        await db.SaveChangesAsync();
        var sp = BuildServices(db, AllowedQuotaEnforcer());

        var result = await DeleteFloatingIpEndpoint.HandleAsync(
            ip.Id,
            sp.GetRequiredService<IFloatingIpService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
        db.FloatingIps.Single().Id.ShouldBe(ip.Id);
    }
}
