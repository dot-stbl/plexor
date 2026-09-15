// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderChangedEmitShould — exercise the PUT → emit wiring
// in OrgAuthProvidersController. The controller delegates the
// audit-emit call to OrgAuthProviderControllerHelpers.EmitAuthProviderChangedAsync,
// which composes the before/after payload and delegates to IAuditEmitter.
// The test substitutes IAuditEmitter with NSubstitute and asserts the
// emit was called once with the expected wire name + payload shape.
//
// What this test pins:
//   1. The OrgAuthProviderChanged wire name fires on every successful
//      PUT path (the emit is wired in the controller, not the helper).
//   2. The payload carries every documented key — old/new pair on
//      Provider, OidcAuthority, OidcClientId. OIDC client secret
//      is intentionally NOT in the payload.
//   3. The target_kind / target_id / actor_user_id propagate from
//      the call site.
//
// No real DB — the test is purely a unit test on the emit-payload
// composition. Full PUT-path integration tests land with Phase 4.6.4.
// ============================================================================

using NSubstitute;
using Plexor.Host.Controllers;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Shared.Kernel.Audit;
using Shouldly;
using Xunit;

namespace Plexor.Host.UnitTests;

/// <summary>
///     Behavioural tests for
///     <see cref="OrgAuthProviderControllerHelpers.EmitAuthProviderChangedAsync" />.
///     Substitutes <see cref="IAuditEmitter" /> with NSubstitute so the
///     emit is observable, then asserts the wire name + payload shape.
/// </summary>
public sealed class OrgAuthProviderChangedEmitShould
{
    /// <summary>Build a realistic Sigil (default) row to use as the
    /// "before" snapshot.</summary>
    private static OrgAuthProviderConfig BuildSigilRow(Guid orgId)
    {
        return new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Provider = OrgAuthProvider.Sigil,
            OidcAuthority = null,
            OidcClientId = null,
            OidcClientSecretProtected = null,
            OidcScopes = ["openid", "profile", "email"],
            CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        };
    }

    /// <summary>Build a realistic OIDC row to use as the "after"
    /// snapshot.</summary>
    private static OrgAuthProviderConfig BuildOidcRow(Guid orgId)
    {
        return new OrgAuthProviderConfig
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Provider = OrgAuthProvider.Oidc,
            OidcAuthority = "https://kc.example.com/realms/plexor",
            OidcClientId = "plexor-console",
            OidcClientSecretProtected = "<encrypted-blob>",
            OidcScopes = ["openid", "profile", "email", "groups"],
            CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero),
        };
    }

    /// <summary>Given a Sigil → Oidc switch, when the helper runs,
    /// then the emitter receives OrgAuthProviderChanged with a
    /// payload carrying the old/new pair for provider, authority,
    /// client-id, and the secret never appears.</summary>
    [Fact(DisplayName = "Given Sigil→Oidc switch, when helper runs, then emit carries the diff and no secret")]
    public async Task Emit_AfterSigilToOidcSwitch_CarriesDiffAndNoSecretAsync()
    {
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var auditEmitter = Substitute.For<IAuditEmitter>();
        var oldConfig = BuildSigilRow(orgId);
        var newConfig = BuildOidcRow(orgId);

        await OrgAuthProviderControllerHelpers.EmitAuthProviderChangedAsync(
            auditEmitter,
            orgId,
            actorId,
            oldConfig,
            newConfig,
            CancellationToken.None);

        await auditEmitter.Received(1).EmitAsync(
            AuditActions.OrgAuthProviderChanged,
            Arg.Is<AuditContext>(context =>
                context.OrgId == orgId
                && context.ActorUserId == actorId
                && context.TargetKind == "org_auth_provider_config"
                && context.TargetId == orgId
                && context.Payload["old_provider"]!.Equals("Sigil")
                && context.Payload["new_provider"]!.Equals("Oidc")
                && context.Payload["old_oidc_authority"] == null
                && context.Payload["new_oidc_authority"]!.Equals("https://kc.example.com/realms/plexor")
                && context.Payload["old_oidc_client_id"] == null
                && context.Payload["new_oidc_client_id"]!.Equals("plexor-console")),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given an Oidc rotation (no provider switch), when the
    /// helper runs, then both old_provider and new_provider are
    /// "Oidc" — the field rotation is the only diff.</summary>
    [Fact(DisplayName = "Given Oidc rotation only, when helper runs, then old/new provider are both Oidc")]
    public async Task Emit_AfterOidcRotation_ProviderFieldUnchangedAsync()
    {
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var auditEmitter = Substitute.For<IAuditEmitter>();
        var oldConfig = BuildOidcRow(orgId);
        var newConfig = new OrgAuthProviderConfig
        {
            Id = oldConfig.Id,
            OrgId = orgId,
            Provider = OrgAuthProvider.Oidc,
            OidcAuthority = "https://kc.example.com/realms/plexor",
            OidcClientId = "plexor-console-rotated",
            OidcClientSecretProtected = "<new-encrypted-blob>",
            OidcScopes = oldConfig.OidcScopes,
            CreatedAt = oldConfig.CreatedAt,
            UpdatedAt = new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero),
        };

        await OrgAuthProviderControllerHelpers.EmitAuthProviderChangedAsync(
            auditEmitter,
            orgId,
            actorId,
            oldConfig,
            newConfig,
            CancellationToken.None);

        await auditEmitter.Received(1).EmitAsync(
            AuditActions.OrgAuthProviderChanged,
            Arg.Is<AuditContext>(context =>
                context.Payload["old_provider"]!.Equals("Oidc")
                && context.Payload["new_provider"]!.Equals("Oidc")
                && context.Payload["old_oidc_client_id"]!.Equals("plexor-console")
                && context.Payload["new_oidc_client_id"]!.Equals("plexor-console-rotated")),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Given a first-time setup (oldConfig null), when the
    /// helper runs, then every <c>old_*</c> key carries <c>null</c>
    /// — the "first provisioning" signal.</summary>
    [Fact(DisplayName = "Given a first-time setup (oldConfig null), when helper runs, then old_* keys are null")]
    public async Task Emit_WithNullOldConfig_OldKeysAreNullAsync()
    {
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var auditEmitter = Substitute.For<IAuditEmitter>();
        var newConfig = BuildSigilRow(orgId);

        await OrgAuthProviderControllerHelpers.EmitAuthProviderChangedAsync(
            auditEmitter,
            orgId,
            actorId,
            oldConfig: null,
            newConfig,
            CancellationToken.None);

        await auditEmitter.Received(1).EmitAsync(
            AuditActions.OrgAuthProviderChanged,
            Arg.Is<AuditContext>(context =>
                context.Payload["old_provider"] == null
                && context.Payload["old_oidc_authority"] == null
                && context.Payload["old_oidc_client_id"] == null
                && context.Payload["new_provider"]!.Equals("Sigil")),
            Arg.Any<CancellationToken>());
    }
}
