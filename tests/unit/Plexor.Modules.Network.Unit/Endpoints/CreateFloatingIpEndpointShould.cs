// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateFloatingIpEndpointShould — exercise the POST
// /api/v1/network/floating-ips handler in isolation. Mirrors the
// Plexor.Modules.Storage.Unit.Endpoints.CreateVolumeEndpointShould
// pattern (real DbContext + NSubstitute ICurrentUser).
//
// Four tests pin the contract:
//   1. Happy path: valid IP + cluster id → 201 Created with the
//      projected body + the row lands in storage with the right
//      org id.
//   2. Validation failure: malformed IP → 400 with a
//      ValidationProblemDetails body.
//   3. Cross-tenant read: GetAsync returns null when the (id, orgId)
//      predicate doesn't match → endpoint returns 404.
//   4. Cross-tenant delete: DeleteAsync returns false when the
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
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    private static ServiceProvider BuildServices(NetworkDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped<IFloatingIpService, EfFloatingIpService>(sp =>
            new EfFloatingIpService(db, TimeProvider.System));
        services.AddScoped<IValidator<CreateFloatingIpRequest>, CreateFloatingIpRequestValidator>();
        return services.BuildServiceProvider();
    }

    private static ICurrentUser StubCurrentUser(Guid tenantId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.TenantId.Returns(tenantId);
        return currentUser;
    }

    /// <summary>Happy path: valid IPv4 → 201 Created with the row
    /// persisted under the caller's tenant.</summary>
    [Fact(DisplayName = "Given a valid CreateFloatingIpRequest, when the handler runs, then the floating IP is created and a 201 Created response is returned")]
    public async Task HappyPath_CreatesFloatingIpAndReturns201Async()
    {
        await using var db = NetworkTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var sp = BuildServices(db);

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
    }

    /// <summary>IPv6 happy path — the validator must accept v6
    /// addresses too.</summary>
    [Fact(DisplayName = "Given a valid IPv6 address, when the handler runs, then the IP is created and persisted")]
    public async Task HappyPath_AcceptsIpv6AddressAsync()
    {
        await using var db = NetworkTestDb.Create();
        var sp = BuildServices(db);

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
    /// + the validator's dictionary.</summary>
    [Fact(DisplayName = "Given an invalid IP address, when the handler runs, then validation fails and the response is 400 BadRequest")]
    public async Task ValidationFailure_Returns400BadRequestAsync()
    {
        await using var db = NetworkTestDb.Create();
        var sp = BuildServices(db);

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
        var sp = BuildServices(db);

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
        var sp = BuildServices(db);

        var result = await DeleteFloatingIpEndpoint.HandleAsync(
            ip.Id,
            sp.GetRequiredService<IFloatingIpService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
        db.FloatingIps.Single().Id.ShouldBe(ip.Id);
    }
}
