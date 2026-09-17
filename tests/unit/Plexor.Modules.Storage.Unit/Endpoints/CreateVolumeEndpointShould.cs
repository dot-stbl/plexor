// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVolumeEndpointShould — exercise the POST /api/v1/storage/volumes
// handler in isolation. Mirrors the audit-endpoint test pattern (real
// DbContext + NSubstitute ICurrentUser) so the handler's validation +
// tenant-scoping logic is asserted end-to-end without standing up a
// real web host.
//
// Four tests pin the contract:
//   1. Happy path: request passes validation, IVolumeService.CreateAsync
//      runs, response is 201 Created with the projected body.
//   2. Validation failure: bad SizeGb (0 or 100_000) → 400 with a
//      ValidationProblemDetails.
//   3. Cross-tenant read: GetAsync returns null when the (id, orgId)
//      predicate doesn't match → endpoint returns 404.
//   4. Cross-tenant delete: DeleteAsync returns false when the
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
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Build a minimal <see cref="IServiceProvider" /> that
    /// resolves the validators + the EF storage service against an
    /// in-memory DbContext. Mirrors the wire-up
    /// <c>AddStorageApiCore</c> + <c>AddStorageInfrastructureCore</c>
    /// would do in <c>Program.cs</c>.</summary>
    private static ServiceProvider BuildServices(StorageDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped<IVolumeService, EfVolumeService>(sp =>
            new EfVolumeService(db, TimeProvider.System));
        services.AddScoped<IValidator<CreateVolumeRequest>, CreateVolumeRequestValidator>();
        return services.BuildServiceProvider();
    }

    private static ICurrentUser StubCurrentUser(Guid tenantId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.TenantId.Returns(tenantId);
        return currentUser;
    }

    /// <summary>Given a valid CreateVolumeRequest, when the handler
    /// runs, then IVolumeService.CreateAsync is invoked with the
    /// caller's org id + the projected fields + the response is
    /// 201 Created.</summary>
    [Fact(DisplayName = "Given a valid CreateVolumeRequest, when the handler runs, then the volume is created and a 201 Created response is returned")]
    public async Task HappyPath_CreatesVolumeAndReturns201Async()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var sp = BuildServices(db);

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
    }

    /// <summary>Given a CreateVolumeRequest with SizeGb = 0 (below
    /// the 1 GiB minimum), when the handler runs, then the validator
    /// rejects the body, IVolumeService.CreateAsync is NOT called,
    /// and the response is 400 BadRequest with a
    /// ValidationProblemDetails body.</summary>
    [Fact(DisplayName = "Given an invalid SizeGb, when the handler runs, then validation fails and the response is 400 BadRequest")]
    public async Task ValidationFailure_Returns400BadRequestAsync()
    {
        await using var db = StorageTestDb.Create();
        var currentUser = StubCurrentUser(Guid.NewGuid());
        var sp = BuildServices(db);

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
        var sp = BuildServices(db);

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
        var sp = BuildServices(db);

        var result = await DeleteVolumeEndpoint.HandleAsync(
            volume.Id,
            sp.GetRequiredService<IVolumeService>(),
            StubCurrentUser(callerOrgId),
            CancellationToken.None);

        result.ShouldBeOfType<NotFound>();
        db.Volumes.Single().Id.ShouldBe(volume.Id);
    }
}
