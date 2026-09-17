// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVolumeEndpointShould — exercise the POST /api/v1/storage/volumes
// handler in isolation. Mirrors the audit-endpoint test pattern (real
// DbContext + NSubstitute ICurrentUser) so the handler's validation +
// tenant-scoping logic is asserted end-to-end without standing up a
// real web host.
//
// Six tests pin the contract:
//   1. Happy path: request passes validation, IVolumeService.CreateAsync
//      runs, the IQuotaEnforcer reserves both storage.volumes.count
//      (amount = 1) + storage.volumes.gb (amount = SizeGb), the
//      response is 201 Created with the projected body, and the row
//      lands in storage with the caller's org.
//   2. Validation failure: bad SizeGb (0) → 400 with a
//      ValidationProblemDetails; no quota reservation, no INSERT.
//   3. Quota denied — count: IQuotaEnforcer returns Denied for
//      storage.volumes.count → QuotaExceededException bubbles out of
//      the endpoint (the global QuotaExceptionHandler maps it to
//      429 in production); no row + no quota counter advance.
//   4. Quota denied — size: IQuotaEnforcer returns Denied for
//      storage.volumes.gb → QuotaExceededException bubbles out of
//      the endpoint; no row + no quota counter advance.
//   5. Cross-tenant read: GetAsync returns null when the (id, orgId)
//      predicate doesn't match → endpoint returns 404.
//   6. Cross-tenant delete: DeleteAsync returns false when the
//      (id, orgId) predicate doesn't match → endpoint returns 404.
// ============================================================================

using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Plexor.Modules.Storage.Api.Endpoints;
using Plexor.Modules.Storage.Api.Models.Requests;
using Plexor.Modules.Storage.Api.Models.Responses;
using Plexor.Modules.Storage.Api.Validation;
using Plexor.Modules.Storage.Application.Volumes;
using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Persistence;
using Plexor.Modules.Storage.Infrastructure.Volumes;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Storage.Unit.Endpoints;

/// <summary>
///     Behavioural tests for
///     <see cref="CreateVolumeEndpoint.HandleAsync" /> + the tenant
///     scoping in <see cref="GetVolumeEndpoint.HandleAsync" /> +
///     <see cref="DeleteVolumeEndpoint.HandleAsync" />. The handler
///     resolves <see cref="IVolumeService" /> (scoped, in-memory
///     here) + <see cref="ICurrentUser" /> (NSubstitute). Validation
///     runs first via the FluentValidation validator resolved from
///     a tiny DI container (the same wire-up the host uses in
///     <c>Program.cs</c>).
/// </summary>
public sealed class CreateVolumeEndpointShould
{
    private static readonly Guid StubActorUserId = Guid.NewGuid();

    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Build a minimal <see cref="IServiceProvider" /> that
    /// resolves the validators + the EF storage service against an
    /// in-memory DbContext. Mirrors the wire-up
    /// <c>AddStorageApiCore</c> + <c>AddStorageInfrastructureCore</c>
    /// would do in <c>Program.cs</c>.</summary>
    private static ServiceProvider BuildServices(StorageDbContext db, IQuotaEnforcer enforcer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(enforcer);
        services.AddScoped<IVolumeService, EfVolumeService>(sp =>
            new EfVolumeService(db, TimeProvider.System, enforcer, StubCurrentUser(Guid.NewGuid())));
        services.AddScoped<IValidator<CreateVolumeRequest>, CreateVolumeRequestValidator>();
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
    /// the existing happy-path tests stay focused on the storage
    /// row path without spinning up the EF enforcer (which requires
    /// a real Postgres). Tests that need to exercise the denied path
    /// swap this for a customised substitute that returns
    /// <see cref="QuotaCheckResult.Denied" /> for the relevant key.</summary>
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

    /// <summary>Given a valid CreateVolumeRequest, when the handler
    /// runs, then IVolumeService.CreateAsync invokes the quota
    /// enforcer for both storage.volumes.count (amount = 1) +
    /// storage.volumes.gb (amount = SizeGb), the INSERT runs, and
    /// the response is 201 Created.</summary>
    [Fact(DisplayName = "Given a valid CreateVolumeRequest, when the handler runs, then the volume is created and a 201 Created response is returned")]
    public async Task HappyPath_CreatesVolumeAndReturns201Async()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var enforcer = AllowedQuotaEnforcer();
        var sp = BuildServices(db, enforcer);

        var request = new CreateVolumeRequest
        {
            ClusterId = Guid.NewGuid(),
            Name = "prod-data-001",
            SizeGb = 256,
        };

        var result = await CreateVolumeEndpoint.HandleAsync(
            request,
            sp.GetRequiredService<IVolumeService>(),
            currentUser,
            sp.GetRequiredService<IValidator<CreateVolumeRequest>>(),
            CancellationToken.None);

        var created = result.ShouldBeOfType<Created<VolumeDetail>>();
        created.Value.ShouldNotBeNull();
        created.Value!.Name.ShouldBe("prod-data-001");
        created.Value.SizeGb.ShouldBe(256);
        created.Value.OrgId.ShouldBe(tenantId);
        created.Value.Status.ShouldBe(nameof(VolumeStatus.Pending));

        // And the row landed in the in-memory DB with the right org.
        var stored = db.Volumes.Single();
        stored.OrgId.ShouldBe(tenantId);
        stored.Name.ShouldBe("prod-data-001");
        stored.SizeGb.ShouldBe(256);
        stored.Status.ShouldBe(VolumeStatus.Pending);

        // The enforcer saw both reservations, scoped to the caller's
        // org with the actor user id from ICurrentUser.
        await enforcer.Received(1).CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope =>
                scope.Kind == QuotaScopeKind.Org &&
                scope.Id == tenantId &&
                scope.OrgId == tenantId &&
                scope.ActorUserId == StubActorUserId),
            QuotaDefinitionKey.VolumesCount,
            1m,
            Arg.Any<CancellationToken>());
        await enforcer.Received(1).CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope =>
                scope.Kind == QuotaScopeKind.Org &&
                scope.Id == tenantId &&
                scope.OrgId == tenantId),
            QuotaDefinitionKey.VolumesGb,
            256m,
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given a CreateVolumeRequest with SizeGb = 0 (below
    /// the 1 GiB minimum), when the handler runs, then the validator
    /// rejects the body, IVolumeService.CreateAsync is NOT called,
    /// the enforcer is NOT invoked, and the response is 400
    /// BadRequest with a ValidationProblemDetails body.</summary>
    [Fact(DisplayName = "Given an invalid SizeGb, when the handler runs, then validation fails and the response is 400 BadRequest")]
    public async Task ValidationFailure_Returns400BadRequestAsync()
    {
        await using var db = StorageTestDb.Create();
        var currentUser = StubCurrentUser(Guid.NewGuid());
        var enforcer = AllowedQuotaEnforcer();
        var sp = BuildServices(db, enforcer);

        var request = new CreateVolumeRequest
        {
            ClusterId = Guid.NewGuid(),
            Name = "bad-volume",
            SizeGb = 0,
        };

        var result = await CreateVolumeEndpoint.HandleAsync(
            request,
            sp.GetRequiredService<IVolumeService>(),
            currentUser,
            sp.GetRequiredService<IValidator<CreateVolumeRequest>>(),
            CancellationToken.None);

        var bad = result.ShouldBeOfType<BadRequest<ValidationProblemDetails>>();
        bad.Value.ShouldNotBeNull();
        bad.Value!.Errors.ShouldContainKey(nameof(CreateVolumeRequest.SizeGb));
        db.Volumes.ShouldBeEmpty();
        await enforcer.DidNotReceive().CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given the IQuotaEnforcer returns Denied for
    /// storage.volumes.count, when the handler runs, then the
    /// enforcer's count check throws QuotaExceededException and the
    /// endpoint propagates it (the global QuotaExceptionHandler
    /// maps it to 429 in production). No row lands in storage and
    /// no second reservation runs.</summary>
    [Fact(DisplayName = "Given storage.volumes.count at limit, when the handler runs, then the endpoint throws QuotaExceededException and no row is created")]
    public async Task CountQuotaDenied_ThrowsQuotaExceededExceptionAsync()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope => scope.Id == tenantId),
            QuotaDefinitionKey.VolumesCount,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Denied(
                Limit: 200m,
                Used: 200m,
                Requested: 1m,
                Reason: "storage.volumes.count at limit"));
        enforcer.CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope => scope.Id == tenantId),
            QuotaDefinitionKey.VolumesGb,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Allowed());
        var sp = BuildServices(db, enforcer);

        var request = new CreateVolumeRequest
        {
            ClusterId = Guid.NewGuid(),
            Name = "over-quota-volume",
            SizeGb = 10,
        };

        var exception = await Should.ThrowAsync<QuotaExceededException>(async () =>
            await CreateVolumeEndpoint.HandleAsync(
                request,
                sp.GetRequiredService<IVolumeService>(),
                currentUser,
                sp.GetRequiredService<IValidator<CreateVolumeRequest>>(),
                CancellationToken.None));

        exception.Limit.ShouldBe(200m);
        exception.Used.ShouldBe(200m);
        exception.Requested.ShouldBe(1m);
        exception.Code.ShouldBe(QuotaExceptions.Exceeded);

        db.Volumes.ShouldBeEmpty();
        // Only the count check ran; the size check was skipped.
        await enforcer.Received(1).CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            QuotaDefinitionKey.VolumesCount,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
        await enforcer.DidNotReceive().CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            QuotaDefinitionKey.VolumesGb,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given the IQuotaEnforcer returns Denied for
    /// storage.volumes.gb, when the handler runs, then the
    /// enforcer's size check throws QuotaExceededException and the
    /// endpoint propagates it. No row lands in storage; the count
    /// reservation that already passed rolls back when the open
    /// transaction disposes on exception.</summary>
    [Fact(DisplayName = "Given storage.volumes.gb at limit, when the handler runs, then the endpoint throws QuotaExceededException and no row is created")]
    public async Task SizeQuotaDenied_ThrowsQuotaExceededExceptionAsync()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            QuotaDefinitionKey.VolumesCount,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Allowed());
        enforcer.CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope => scope.Id == tenantId),
            QuotaDefinitionKey.VolumesGb,
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Denied(
                Limit: 1024m,
                Used: 1024m,
                Requested: 256m,
                Reason: "storage.volumes.gb at limit"));
        var sp = BuildServices(db, enforcer);

        var request = new CreateVolumeRequest
        {
            ClusterId = Guid.NewGuid(),
            Name = "too-big-volume",
            SizeGb = 256,
        };

        var exception = await Should.ThrowAsync<QuotaExceededException>(async () =>
            await CreateVolumeEndpoint.HandleAsync(
                request,
                sp.GetRequiredService<IVolumeService>(),
                currentUser,
                sp.GetRequiredService<IValidator<CreateVolumeRequest>>(),
                CancellationToken.None));

        exception.Limit.ShouldBe(1024m);
        exception.Used.ShouldBe(1024m);
        exception.Requested.ShouldBe(256m);
        exception.Code.ShouldBe(QuotaExceptions.Exceeded);

        db.Volumes.ShouldBeEmpty();
    }

    /// <summary>Given a volume row that belongs to a different org,
    /// when the get-by-id endpoint runs for the caller's org, then
    /// the response is 404 — not 403 — so the caller can't enumerate
    /// org ids by probing.</summary>
    [Fact(DisplayName = "Given a volume in another org, when the get-by-id endpoint runs for the caller's org, then the response is 404 NotFound")]
    public async Task CrossTenant_Get_Returns404NotFoundAsync()
    {
        await using var db = StorageTestDb.Create();
        var ownerOrgId = Guid.NewGuid();
        var callerOrgId = Guid.NewGuid();
        var volume = new Volume
        {
            Id = Guid.NewGuid(),
            OrgId = ownerOrgId,
            ClusterId = Guid.NewGuid(),
            Name = "other-org-volume",
            SizeGb = 100,
            Status = VolumeStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.Volumes.AddAsync(volume);
        await db.SaveChangesAsync();
        var sp = BuildServices(db, AllowedQuotaEnforcer());

        var result = await GetVolumeEndpoint.HandleAsync(
            volume.Id,
            sp.GetRequiredService<IVolumeService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
    }

    /// <summary>Given a volume row that belongs to a different org,
    /// when the delete endpoint runs for the caller's org, then
    /// the response is 404 — not 403 — and the row stays in the
    /// database (the caller's tenant did not delete the owner's
    /// resource).</summary>
    [Fact(DisplayName = "Given a volume in another org, when the delete endpoint runs for the caller's org, then the response is 404 and the row is not deleted")]
    public async Task CrossTenant_Delete_Returns404NotFoundAndKeepsRowAsync()
    {
        await using var db = StorageTestDb.Create();
        var ownerOrgId = Guid.NewGuid();
        var callerOrgId = Guid.NewGuid();
        var volume = new Volume
        {
            Id = Guid.NewGuid(),
            OrgId = ownerOrgId,
            ClusterId = Guid.NewGuid(),
            Name = "other-org-volume",
            SizeGb = 100,
            Status = VolumeStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        };
        await db.Volumes.AddAsync(volume);
        await db.SaveChangesAsync();
        var sp = BuildServices(db, AllowedQuotaEnforcer());

        var result = await DeleteVolumeEndpoint.HandleAsync(
            volume.Id,
            sp.GetRequiredService<IVolumeService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
        db.Volumes.Single().Id.ShouldBe(volume.Id);
    }
}
