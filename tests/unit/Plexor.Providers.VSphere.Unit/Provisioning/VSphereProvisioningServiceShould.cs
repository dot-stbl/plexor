// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereProvisioningServiceShould — exercise the provisioning
// service in isolation. Pins:
//   1. Success path: a successful Refit call writes an audit row
//      with Status = "SUCCESS" + the returned VM mo-ref.
//   2. ApiException path: a non-success Refit response writes a
//      "FAILED" row + surfaces a result with Status = "FAILED".
//   3. Audit row written even on failure (the operator can
//      reconstruct "who tried to clone what").
// ============================================================================

// The test namespace Plexor.Providers.VSphere.Unit.Provisioning shadows
// the production namespace Plexor.Providers.VSphere.Inventory when
// resolving "Inventory.*" — disambiguate via global::.
using global::Plexor.Providers.VSphere.Inventory.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.Providers.VSphere.Infrastructure.Provisioning;
using Plexor.Providers.VSphere.Provisioning;
using Shouldly;
using Xunit;

namespace Plexor.Providers.VSphere.Unit.Provisioning;

/// <summary>
///     Behavioural tests for
///     <see cref="VSphereProvisioningService" />. The service calls
///     the Refit client to issue the vCenter clone + writes an
///     audit row. The tests substitute the Refit client so the
///     vCenter network calls are bypassed; the in-memory
///     DbContext captures the audit rows.
/// </summary>
public sealed class VSphereProvisioningServiceShould
{
    /// <summary>
    ///     Given a successful Refit call, when
    ///     CloneTemplateAsync runs, then the audit row is written
    ///     with Status = "SUCCESS" + the returned VM mo-ref, and
    ///     the result carries the same fields.
    /// </summary>
    [Fact(DisplayName = "Given successful Refit call, when CloneTemplateAsync runs, then writes SUCCESS audit row")]
    public async Task CloneTemplateAsyncOnSuccessWritesSuccessAuditRowAsync()
    {
        var client = Substitute.For<IVSphereClient>();
        client.CloneTemplateAsync(Arg.Any<VSphereCloneRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VCentreTaskHandle
            {
                TaskMoref = "task-1",
                ResultVmMoref = "vm-new-123",
                Status = "SUCCESS",
            });

        await using var db = await VSphereTestDb.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var sut = new VSphereProvisioningService(client, db, clock, NullLogger<VSphereProvisioningService>.Instance);

        var result = await sut.CloneTemplateAsync(
            sourceTemplateMoref: "vm-42",
            name: "new-vm",
            targetFolderMoref: "folder-99",
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("SUCCESS");
        result.VmMoref.ShouldBe("vm-new-123");
        result.RunId.ShouldNotBe(Guid.Empty);

        var run = await db.ProvisioningRuns.FindAsync(result.RunId);
        run.ShouldNotBeNull();
        run.Status.ShouldBe("SUCCESS");
        run.SourceTemplateMoref.ShouldBe("vm-42");
        run.RequestedName.ShouldBe("new-vm");
        run.TargetFolderMoref.ShouldBe("folder-99");
        run.ResultVmMoref.ShouldBe("vm-new-123");
        run.FinishedAt.ShouldBe(clock.GetUtcNow());
        run.ErrorMessage.ShouldBeNull();
    }

    /// <summary>
    ///     Given an upstream Refit failure, when
    ///     CloneTemplateAsync runs, then the audit row is written
    ///     with Status = "FAILED" + the error message, and the
    ///     result carries Status = "FAILED" with a null VM mo-ref.
    /// </summary>
    [Fact(DisplayName = "Given Refit failure, when CloneTemplateAsync runs, then writes FAILED audit row")]
    public async Task CloneTemplateAsyncOnRefitFailureWritesFailedAuditRowAsync()
    {
        var client = Substitute.For<IVSphereClient>();
        client.CloneTemplateAsync(Arg.Any<VSphereCloneRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<VCentreTaskHandle>(new HttpRequestException("synthetic upstream failure")));

        await using var db = await VSphereTestDb.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sut = new VSphereProvisioningService(client, db, clock, NullLogger<VSphereProvisioningService>.Instance);

        var result = await sut.CloneTemplateAsync(
            sourceTemplateMoref: "vm-42",
            name: "new-vm",
            targetFolderMoref: null,
            cancellationToken: CancellationToken.None);

        result.Status.ShouldBe("FAILED");
        result.VmMoref.ShouldBeNull();
        result.RunId.ShouldNotBe(Guid.Empty);

        var run = await db.ProvisioningRuns.FindAsync(result.RunId);
        run.ShouldNotBeNull();
        run.Status.ShouldBe("FAILED");
        run.ResultVmMoref.ShouldBeNull();
        run.ErrorMessage.ShouldNotBeNull();
        run.ErrorMessage.ShouldContain("network error");
    }
}
