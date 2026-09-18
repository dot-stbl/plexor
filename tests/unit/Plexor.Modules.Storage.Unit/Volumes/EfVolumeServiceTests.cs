// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfVolumeServiceTests — exercise EfVolumeService.UpdateSizeAsync against
// an in-memory StorageDbContext. Two test cases pin the 4.5.d
// resize-quota contract:
//
//   1. Growing a volume past the effective storage.volumes.gb limit
//      throws QuotaExceededException and the row's SizeGb is NOT
//      updated (the open transaction rolls back on dispose).
//   2. Shrinking a volume (or no-change) does NOT call the enforcer —
//      the UPDATE applies normally.
//
// Mirrors CreateVolumeEndpointShould's IQuotaEnforcer stub pattern
// (real in-memory DbContext + NSubstitute ICurrentUser +
// IQuotaEnforcer) so the resize path's transactional shape is
// asserted end-to-end without standing up a real web host.
// ============================================================================

using NSubstitute;
using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Persistence;
using Plexor.Modules.Storage.Infrastructure.Volumes;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Storage.Unit.Volumes;

/// <summary>
///     Behavioural tests for <see cref="EfVolumeService.UpdateSizeAsync" />.
///     The resize path must reserve the storage.volumes.gb delta against
///     the org scope; a Denied throws <see cref="QuotaExceededException" />
///     and the row stays at its prior SizeGb.
/// </summary>
public sealed class EfVolumeServiceTests
{
    private static readonly Guid StubActorUserId = Guid.NewGuid();

    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>NSubstitute-backed <see cref="IQuotaEnforcer" /> that
    /// always returns <see cref="QuotaCheckResult.Allowed" />. Tests
    /// that need to exercise the denied path swap this for a
    /// customised substitute that returns
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

    /// <summary>NSubstitute-backed <see cref="ICurrentUser" /> that
    /// returns a stable stub user id + a caller-supplied tenant id.</summary>
    private static ICurrentUser StubCurrentUser(Guid tenantId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(StubActorUserId);
        currentUser.TenantId.Returns(tenantId);
        return currentUser;
    }

    /// <summary>Seed a single volume row in the target org with the
    /// given starting SizeGb. Returns the volume id so the test can
    /// drive <c>UpdateSizeAsync(volumeId, orgId, newSizeGb, ct)</c>.</summary>
    private static async Task<Guid> SeedVolumeAsync(
        StorageDbContext db,
        Guid orgId,
        int sizeGb)
    {
        var volumeId = Guid.NewGuid();
        await db.Volumes.AddAsync(new Volume
        {
            Id = volumeId,
            OrgId = orgId,
            ClusterId = Guid.NewGuid(),
            Name = $"vol-{Guid.NewGuid():N}",
            SizeGb = sizeGb,
            Status = VolumeStatus.Attached,
            CreatedAt = FixedNow,
            UpdatedAt = FixedNow,
        });
        await db.SaveChangesAsync();
        return volumeId;
    }

    /// <summary>Given a 100 GiB volume and an enforcer that returns
    /// <see cref="QuotaCheckResult.Denied" /> with limit=1024 used=1024
    /// requested=256, when UpdateSizeAsync grows the volume to 356
    /// GiB, then the enforcer sees a storage.volumes.gb reservation
    /// with amount=256, the call throws
    /// <see cref="QuotaExceededException" /> with the enforcer's
    /// numbers, and the row's SizeGb stays at 100 (the open
    /// transaction rolls back on dispose — no UPDATE applied).</summary>
    [Fact(DisplayName = "Given a volume grow that exceeds storage.volumes.gb, when UpdateSizeAsync runs, then it throws QuotaExceededException and the row's SizeGb is unchanged")]
    public async Task UpdateSizeAsync_WhenGrowthExceedsQuota_ThrowsQuotaExceededExceptionAsync()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var volumeId = await SeedVolumeAsync(db, tenantId, sizeGb: 100);

        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Denied(
                Limit: 1024m,
                Used: 1024m,
                Requested: 256m,
                Reason: "storage.volumes.gb at limit"));
        var sut = new EfVolumeService(db, TimeProvider.System, enforcer, currentUser);

        var exception = await Should.ThrowAsync<QuotaExceededException>(
            async () => await sut.UpdateSizeAsync(volumeId, tenantId, newSizeGb: 356, CancellationToken.None));

        exception.Limit.ShouldBe(1024m);
        exception.Used.ShouldBe(1024m);
        exception.Requested.ShouldBe(256m);
        exception.Code.ShouldBe(QuotaExceptions.Exceeded);

        // The reservation was scoped to the caller's org with the
        // actor user id from ICurrentUser; amount is the delta
        // (new - old = 356 - 100 = 256), NOT the new total.
        await enforcer.Received(1).CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope =>
                scope.Kind == QuotaScopeKind.Org &&
                scope.Id == tenantId &&
                scope.OrgId == tenantId &&
                scope.ActorUserId == StubActorUserId),
            QuotaDefinitionKey.VolumesGb,
            256m,
            Arg.Any<CancellationToken>());

        // No UPDATE applied — the row stays at the seeded SizeGb.
        var stored = db.Volumes.Single(volume => volume.Id == volumeId);
        stored.SizeGb.ShouldBe(100);
    }

    /// <summary>Given a 1024 GiB volume and an enforcer that would
    /// deny anything, when UpdateSizeAsync shrinks the volume to 100
    /// GiB, then the enforcer is NOT called (a negative delta never
    /// fails on a capacity quota), the UPDATE applies, and the row
    /// reflects the new SizeGb.</summary>
    [Fact(DisplayName = "Given a volume shrink, when UpdateSizeAsync runs, then the enforcer is not called and the row's SizeGb drops to the new value")]
    public async Task UpdateSizeAsync_WhenShrinking_DoesNotCallEnforcerAsync()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var volumeId = await SeedVolumeAsync(db, tenantId, sizeGb: 1024);

        // Customised enforcer that would deny anything — proves the
        // shrink path never reaches it.
        var enforcer = Substitute.For<IQuotaEnforcer>();
        enforcer.CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult.Denied(
                Limit: 0m,
                Used: 0m,
                Requested: 1m,
                Reason: "would have failed if invoked"));
        var sut = new EfVolumeService(db, TimeProvider.System, enforcer, currentUser);

        var updated = await sut.UpdateSizeAsync(volumeId, tenantId, newSizeGb: 100, CancellationToken.None);

        updated.ShouldNotBeNull();
        updated!.SizeGb.ShouldBe(100);

        // Shrinking frees capacity; the enforcer never sees a reservation.
        await enforcer.DidNotReceive().CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());

        var stored = db.Volumes.Single(volume => volume.Id == volumeId);
        stored.SizeGb.ShouldBe(100);
    }

    /// <summary>Given a 100 GiB volume and an enforcer that returns
    /// <see cref="QuotaCheckResult.Allowed" />, when UpdateSizeAsync
    /// grows the volume to 200 GiB, then the enforcer sees a
    /// reservation for the delta (100 GiB) and the row's SizeGb is
    /// 200.</summary>
    [Fact(DisplayName = "Given a volume grow within quota, when UpdateSizeAsync runs, then the enforcer reserves the delta and the row's SizeGb updates")]
    public async Task UpdateSizeAsync_WhenGrowthWithinQuota_ReservesDeltaAndUpdatesAsync()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var volumeId = await SeedVolumeAsync(db, tenantId, sizeGb: 100);
        var enforcer = AllowedQuotaEnforcer();
        var sut = new EfVolumeService(db, TimeProvider.System, enforcer, currentUser);

        var updated = await sut.UpdateSizeAsync(volumeId, tenantId, newSizeGb: 200, CancellationToken.None);

        updated.ShouldNotBeNull();
        updated!.SizeGb.ShouldBe(200);

        await enforcer.Received(1).CheckAndReserveAsync(
            Arg.Is<QuotaScope>(scope =>
                scope.Kind == QuotaScopeKind.Org &&
                scope.Id == tenantId &&
                scope.OrgId == tenantId &&
                scope.ActorUserId == StubActorUserId),
            QuotaDefinitionKey.VolumesGb,
            100m,
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given a 100 GiB volume and an enforcer that returns
    /// <see cref="QuotaCheckResult.Allowed" />, when UpdateSizeAsync
    /// is called with the same size, then the enforcer is NOT called
    /// (zero delta is a no-op write) and the row is unchanged.</summary>
    [Fact(DisplayName = "Given a no-change resize, when UpdateSizeAsync runs, then the enforcer is not called and the row's SizeGb stays the same")]
    public async Task UpdateSizeAsync_WhenNoChange_DoesNotCallEnforcerAsync()
    {
        await using var db = StorageTestDb.Create();
        var tenantId = Guid.NewGuid();
        var currentUser = StubCurrentUser(tenantId);
        var volumeId = await SeedVolumeAsync(db, tenantId, sizeGb: 100);
        var enforcer = AllowedQuotaEnforcer();
        var sut = new EfVolumeService(db, TimeProvider.System, enforcer, currentUser);

        var updated = await sut.UpdateSizeAsync(volumeId, tenantId, newSizeGb: 100, CancellationToken.None);

        updated.ShouldNotBeNull();
        updated!.SizeGb.ShouldBe(100);

        await enforcer.DidNotReceive().CheckAndReserveAsync(
            Arg.Any<QuotaScope>(),
            Arg.Any<QuotaDefinitionKey>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>());
    }
}