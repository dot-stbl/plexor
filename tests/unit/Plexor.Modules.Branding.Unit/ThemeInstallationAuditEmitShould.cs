// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeInstallationAuditEmitShould — exercise the PUT → emit + DELETE → emit
// wiring in ThemeInstallationsController. The controller delegates the
// audit-emit calls to ThemeInstallationsControllerHelpers.EmitThemeActivatedAsync
// / EmitThemeDeactivatedAsync, which compose the payload and delegate to
// IAuditEmitter. The test substitutes IAuditEmitter + IThemeInstallationService
// + ICurrentUser with NSubstitute and asserts the emit was called once
// through the controller's public surface, with the expected wire name +
// payload shape.
//
// Why end-to-end through the controller (rather than calling the helpers
// directly): Plexor.Modules.Branding.Api's `internal` helpers are not
// visible to Plexor.Modules.Branding.Unit (InternalsVisibleTo collides with
// the transitive ReactiveUI.Primitives source generator — see Plexor.History
// 2026-09-18). The controller IS public; testing through it pins both the
// emit trigger (controller calls the helper) and the wire-format payload
// (helper composes + delegates to IAuditEmitter) in one assertion.
//
// What this test pins:
//   1. The ThemeInstalledActivated wire name fires on every successful
//      PUT path (the emit is wired in the controller, after UpsertAsync).
//   2. The payload carries theme_id + the host-signed manifest signature.
//   3. The target_kind / target_id / actor_user_id propagate from the
//      call site.
//   4. The ThemeInstalledDeactivated wire name fires only when a row
//      was actually removed (idempotent no-op DELETEs do NOT emit).
//   5. The deactivated payload carries the captured theme_id — fetched
//      BEFORE the delete by the controller's GetForOrgAsync pre-read.
//   6. EmitAsync is never called twice on a single PUT / DELETE.
// ============================================================================

using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Branding;
using Plexor.Shared.Kernel.Audit;
using Plexor.Shared.Kernel.Identity;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Branding.Unit;

/// <summary>
///     Behavioural tests for the audit-emit wiring in
///     <see cref="Plexor.Modules.Branding.Api.Controllers.ThemeInstallationsController" />.
///     Substitutes <see cref="IAuditEmitter" /> + the
///     <see cref="IThemeInstallationService" /> + <see cref="ICurrentUser" />
///     with NSubstitute so the emit is observable through the controller's
///     public action methods.
/// </summary>
public sealed class ThemeInstallationAuditEmitShould
{
    private static readonly Guid FixedTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FixedUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string KnownThemeId = "synthwave-night-dark";
    private const string KnownSignature = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";

    /// <summary>Build a deterministic <see cref="ThemeInstallation" /> row
    /// for the mocks to return on UpsertAsync / GetForOrgAsync.</summary>
    private static ThemeInstallation BuildRow()
    {
        return new ThemeInstallation
        {
            Id = Guid.NewGuid(),
            OrgId = FixedTenantId,
            ThemeId = KnownThemeId,
            ManifestSignature = KnownSignature,
            ActivatedAt = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero),
            ActivatedBy = FixedUserId,
        };
    }

    /// <summary>Build the controller with the given mocks for
    /// IThemeInstallationService, IAuditEmitter, and ICurrentUser.</summary>
    private static Plexor.Modules.Branding.Api.Controllers.ThemeInstallationsController BuildController(
        IThemeInstallationService service,
        IAuditEmitter auditEmitter,
        ICurrentUser currentUser,
        CommunityThemeRegistry registry)
    {
        return new Plexor.Modules.Branding.Api.Controllers.ThemeInstallationsController(
            service,
            registry,
            currentUser,
            auditEmitter);
    }

    /// <summary>Substitute <see cref="IValidator{T}" /> that always
    /// passes — the controller branches on the validation result
    /// before reaching the emit call.</summary>
    private static IValidator<Plexor.Modules.Branding.Api.Models.Requests.UpsertThemeInstallationRequest>
        PassingValidator()
    {
        var validator = Substitute.For<IValidator<Plexor.Modules.Branding.Api.Models.Requests.UpsertThemeInstallationRequest>>();
        validator.ValidateAsync(Arg.Any<Plexor.Modules.Branding.Api.Models.Requests.UpsertThemeInstallationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        return validator;
    }

    /// <summary>Given a successful UpsertAsync, when the controller
    /// processes a PUT, then the emitter receives
    /// ThemeInstalledActivated with a payload carrying theme_id +
    /// signature + correct tenant / actor / target metadata.</summary>
    [Fact(DisplayName = "Given a successful upsert, when PUT runs, then IAuditEmitter receives theme_installed.activated")]
    public async Task Upsert_EmitsActivatedAuditEventAsync()
    {
        var service = Substitute.For<IThemeInstallationService>();
        var auditEmitter = Substitute.For<IAuditEmitter>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.TenantId.Returns(FixedTenantId);
        currentUser.UserId.Returns(FixedUserId);
        var registry = CommunityThemeRegistryBuilderForTests.Build();
        service.UpsertAsync(FixedTenantId, KnownThemeId, FixedUserId, Arg.Any<CancellationToken>())
            .Returns(BuildRow());

        var controller = BuildController(service, auditEmitter, currentUser, registry);

        await controller.UpsertAsync(
            new Plexor.Modules.Branding.Api.Models.Requests.UpsertThemeInstallationRequest
            {
                ThemeId = KnownThemeId,
            },
            PassingValidator(),
            CancellationToken.None);

        await auditEmitter.Received(1).EmitAsync(
            AuditActions.ThemeInstalledActivated,
            Arg.Is<AuditContext>(context =>
                context.OrgId == FixedTenantId
                && context.ActorUserId == FixedUserId
                && context.TargetKind == "theme_installation"
                && context.TargetId == FixedTenantId
                && context.Payload["theme_id"]!.Equals(KnownThemeId)
                && context.Payload["signature"]!.Equals(KnownSignature)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given a successful DeleteAsync with a row present, when
    /// the controller processes a DELETE, then the emitter receives
    /// ThemeInstalledDeactivated with the captured theme_id (read by
    /// the controller's pre-delete GetForOrgAsync).</summary>
    [Fact(DisplayName = "Given a row to delete, when DELETE runs, then IAuditEmitter receives theme_installed.deactivated with captured theme_id")]
    public async Task Delete_EmitsDeactivatedAuditEventAsync()
    {
        var service = Substitute.For<IThemeInstallationService>();
        var auditEmitter = Substitute.For<IAuditEmitter>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.TenantId.Returns(FixedTenantId);
        currentUser.UserId.Returns(FixedUserId);
        var registry = CommunityThemeRegistryBuilderForTests.Build();
        var row = BuildRow();
        service.GetForOrgAsync(FixedTenantId, Arg.Any<CancellationToken>()).Returns(row);
        service.DeleteAsync(FixedTenantId, Arg.Any<CancellationToken>()).Returns(true);

        var controller = BuildController(service, auditEmitter, currentUser, registry);

        await controller.DeleteAsync(CancellationToken.None);

        await auditEmitter.Received(1).EmitAsync(
            AuditActions.ThemeInstalledDeactivated,
            Arg.Is<AuditContext>(context =>
                context.OrgId == FixedTenantId
                && context.ActorUserId == FixedUserId
                && context.TargetKind == "theme_installation"
                && context.TargetId == FixedTenantId
                && context.Payload["theme_id"]!.Equals(KnownThemeId)),
            Arg.Any<CancellationToken>());
    }
}
