// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfBrandingServiceShould — exercise the IBrandingService in isolation.
// The service is the only place that knows how to merge the global
// row + per-org override into the resolved boot config — this test
// pins the merge semantics so a future refactor doesn't silently
// change the resolution rules.
// ============================================================================

using NSubstitute;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Domain.Entities;
using Plexor.Modules.Branding.Infrastructure.Branding;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Branding.Unit;

/// <summary>
///     Behavioural tests for <see cref="EfBrandingService" />. Covers
///     the read + upsert + delete paths on both rows and the merge
///     resolution that powers <c>GET /api/v1/branding/boot</c>.
/// </summary>
public sealed class EfBrandingServiceShould
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static EfBrandingService BuildService(out FakeClock clock)
    {
        clock = new FakeClock(FixedNow);
        return new EfBrandingService(BrandingTestDb.Create(), clock);
    }

    /// <summary>Given an empty database, when GetGlobalAsync runs,
    /// then a sentinel default row is returned (the seeder hasn't
    /// run yet).</summary>
    [Fact(DisplayName = "Given empty DB, when GetGlobalAsync runs, then returns sentinel defaults")]
    public async Task GetGlobal_WithEmptyDb_ReturnsSentinelAsync()
    {
        var service = BuildService(out _);

        var row = await service.GetGlobalAsync();

        row.Id.ShouldBe(GlobalThemeConfig.SingletonId);
        row.BrandName.ShouldBe("Plexor");
        row.DefaultPresetId.ShouldBe("plexor-default-light");
    }

    /// <summary>Given an upsert, when GetGlobalAsync runs, then the
    /// upserted values come back.</summary>
    [Fact(DisplayName = "Given an upsert, when GetGlobalAsync runs, then returns the upserted values")]
    public async Task UpsertGlobal_RoundTripsAsync()
    {
        var service = BuildService(out var clock);
        var actorId = Guid.NewGuid();

        var upserted = await service.UpsertGlobalAsync(
            new GlobalThemeConfig
            {
                BrandName = "Acme Corp",
                DefaultPresetId = "plexor-noir",
                BrandLogoUrl = "https://acme.example/logo.svg",
                CustomAccent = "oklch(0.65 0.18 250)",
            },
            actorUserId: actorId);

        upserted.BrandName.ShouldBe("Acme Corp");
        upserted.DefaultPresetId.ShouldBe("plexor-noir");
        upserted.BrandLogoUrl.ShouldBe("https://acme.example/logo.svg");
        upserted.CustomAccent.ShouldBe("oklch(0.65 0.18 250)");
        upserted.UpdatedBy.ShouldBe(actorId);
        upserted.UpdatedAt.ShouldBe(clock.GetUtcNow());

        var readBack = await service.GetGlobalAsync();
        readBack.BrandName.ShouldBe("Acme Corp");
        readBack.UpdatedBy.ShouldBe(actorId);
    }

    /// <summary>Given a per-org upsert with no global, when
    /// ResolveForOrgAsync runs, then the per-org row wins on every
    /// field.</summary>
    [Fact(DisplayName = "Given per-org override + no global, when ResolveForOrgAsync runs, then per-org wins everywhere")]
    public async Task ResolveForOrg_PerOrgWinsWhenGlobalEmptyAsync()
    {
        var service = BuildService(out _);
        var orgId = Guid.NewGuid();

        await service.UpsertOrgOverrideAsync(
            orgId,
            new OrgThemeConfig
            {
                BrandName = "Tenant Brand",
                PresetId = "plexor-noir",
                BrandLogoUrl = "https://tenant/logo.svg",
                CustomAccent = "oklch(0.5 0.2 200)",
            },
            actorUserId: null);

        var resolved = await service.ResolveForOrgAsync(orgId);

        resolved.BrandName.ShouldBe("Tenant Brand");
        resolved.DefaultPresetId.ShouldBe("plexor-noir");
        resolved.BrandLogoUrl.ShouldBe("https://tenant/logo.svg");
        resolved.CustomAccent.ShouldBe("oklch(0.5 0.2 200)");
    }

    /// <summary>Given a per-org override with partial fields, when
    /// ResolveForOrgAsync runs, then per-org wins on non-null fields
    /// and global wins on null fields.</summary>
    [Fact(DisplayName = "Given partial per-org + global, when ResolveForOrgAsync runs, then merge is per-org-wins-on-non-null")]
    public async Task ResolveForOrg_PartialOverrideMergesWithGlobalAsync()
    {
        var service = BuildService(out _);
        var orgId = Guid.NewGuid();

        await service.UpsertGlobalAsync(
            new GlobalThemeConfig
            {
                BrandName = "Operator Brand",
                DefaultPresetId = "plexor-default-light",
                BrandLogoUrl = "https://operator/logo.svg",
                CustomAccent = "oklch(0.3 0.02 255)",
            },
            actorUserId: null);

        // Override only the brand name + preset; leave logo + accent
        // as inherit (null).
        await service.UpsertOrgOverrideAsync(
            orgId,
            new OrgThemeConfig
            {
                BrandName = "Tenant Brand",
                PresetId = "plexor-noir",
                BrandLogoUrl = null,
                CustomAccent = null,
            },
            actorUserId: null);

        var resolved = await service.ResolveForOrgAsync(orgId);

        resolved.BrandName.ShouldBe("Tenant Brand");
        resolved.DefaultPresetId.ShouldBe("plexor-noir");
        resolved.BrandLogoUrl.ShouldBe("https://operator/logo.svg");
        resolved.CustomAccent.ShouldBe("oklch(0.3 0.02 255)");
    }

    /// <summary>Given no org override, when ResolveForOrgAsync runs,
    /// then the global row is returned verbatim.</summary>
    [Fact(DisplayName = "Given no org override, when ResolveForOrgAsync runs, then global is returned")]
    public async Task ResolveForOrg_NoOverrideReturnsGlobalAsync()
    {
        var service = BuildService(out _);
        await service.UpsertGlobalAsync(
            new GlobalThemeConfig
            {
                BrandName = "Operator Brand",
                DefaultPresetId = "plexor-default-light",
                BrandLogoUrl = "https://operator/logo.svg",
            },
            actorUserId: null);

        var resolved = await service.ResolveForOrgAsync(Guid.NewGuid());

        resolved.BrandName.ShouldBe("Operator Brand");
        resolved.DefaultPresetId.ShouldBe("plexor-default-light");
        resolved.BrandLogoUrl.ShouldBe("https://operator/logo.svg");
        resolved.CustomAccent.ShouldBeNull();
    }

    /// <summary>Given an existing per-org override, when
    /// DeleteOrgOverrideAsync runs, then the row is gone and
    /// subsequent reads return null.</summary>
    [Fact(DisplayName = "Given an existing org override, when DeleteOrgOverrideAsync runs, then the row is removed")]
    public async Task DeleteOrgOverride_RemovesRowAsync()
    {
        var service = BuildService(out _);
        var orgId = Guid.NewGuid();

        await service.UpsertOrgOverrideAsync(
            orgId,
            new OrgThemeConfig { BrandName = "To Be Deleted" },
            actorUserId: null);

        var deleted = await service.DeleteOrgOverrideAsync(orgId);

        deleted.ShouldBeTrue();
        var readBack = await service.GetOrgOverrideAsync(orgId);
        readBack.ShouldBeNull();
    }

    /// <summary>Given no existing per-org override, when
    /// DeleteOrgOverrideAsync runs, then the call returns false
    /// (idempotent).</summary>
    [Fact(DisplayName = "Given no existing org override, when DeleteOrgOverrideAsync runs, then returns false")]
    public async Task DeleteOrgOverride_MissingRowReturnsFalseAsync()
    {
        var service = BuildService(out _);

        var deleted = await service.DeleteOrgOverrideAsync(Guid.NewGuid());

        deleted.ShouldBeFalse();
    }
}