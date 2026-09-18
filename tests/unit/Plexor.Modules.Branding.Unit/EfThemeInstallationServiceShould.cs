// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfThemeInstallationServiceShould — exercise the
// IThemeInstallationService over an in-memory BrandingDbContext +
// an HMAC verifier wired to a deterministic-purpose
// IDataProtectionProvider. The InMemory provider ignores
// PostgreSQL-specific column types (text vs character varying)
// — fine for the read/upsert/delete semantics under test.
// ============================================================================

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Infrastructure.Branding;
using Plexor.Modules.Branding.Infrastructure.ThemeManifests;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Branding.Unit;

/// <summary>
///     Behavioural tests for <see cref="EfThemeInstallationService" />.
///     Covers the per-org install / read / delete lifecycle and the
///     verify-before-upsert invariant.
/// </summary>
public sealed class EfThemeInstallationServiceShould
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static EfThemeInstallationService BuildService()
    {
        var clock = new FakeClock(FixedNow);
        var registry = CommunityThemeRegistryBuilderForTests.Build();
        var verifier = new HmacThemeManifestVerifier(TestDataProtectionProviderFactory.Create());
        return new EfThemeInstallationService(
            BrandingTestDb.Create(),
            clock,
            verifier,
            registry);
    }

    /// <summary>Given an empty DB, when GetForOrgAsync runs, then
    /// null is returned (the controller maps that to a 404).</summary>
    [Fact(DisplayName = "Given empty DB, when GetForOrgAsync runs, then returns null")]
    public async Task GetForOrg_WhenEmpty_ReturnsNullAsync()
    {
        var service = BuildService();

        var row = await service.GetForOrgAsync(Guid.NewGuid());

        row.ShouldBeNull();
    }

    /// <summary>Given a valid upsert, when GetForOrgAsync runs,
    /// then the row comes back with the persisted signature +
    /// actor.</summary>
    [Fact(DisplayName = "Given an upsert, when GetForOrgAsync runs, then the row comes back with the host-signed signature + actor")]
    public async Task Upsert_ThenGet_RoundTripsAsync()
    {
        var service = BuildService();
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var upserted = await service.UpsertAsync(orgId, "synthwave-night-dark", actorId);

        upserted.OrgId.ShouldBe(orgId);
        upserted.ThemeId.ShouldBe("synthwave-night-dark");
        upserted.ManifestSignature.ShouldNotBeNullOrWhiteSpace();
        upserted.ManifestSignature.Length.ShouldBe(64);
        upserted.ActivatedBy.ShouldBe(actorId);
        upserted.ActivatedAt.ShouldBe(FixedNow);

        var readBack = await service.GetForOrgAsync(orgId);
        readBack.ShouldNotBeNull();
        readBack.ManifestSignature.ShouldBe(upserted.ManifestSignature);
    }

    /// <summary>Given an existing installation, when UpsertAsync
    /// runs again with a new theme id, then the row updates in
    /// place (same row id, new values).</summary>
    [Fact(DisplayName = "Given an existing installation, when UpsertAsync runs with a new themeId, then the row updates in place")]
    public async Task Upsert_ExistingInstallation_UpdatesInPlaceAsync()
    {
        var service = BuildService();
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var first = await service.UpsertAsync(orgId, "synthwave-night-dark", actorId);
        var second = await service.UpsertAsync(orgId, "paper-light", actorId);

        second.Id.ShouldBe(first.Id);
        second.ThemeId.ShouldBe("paper-light");
        second.ManifestSignature.ShouldNotBe(first.ManifestSignature);
    }

    /// <summary>Given a published theme id, when UpsertAsync
    /// runs, then the persisted signature is hex-encoded +
    /// 64-char (HMAC-SHA256).</summary>
    [Fact(DisplayName = "Given a published themeId, when UpsertAsync runs, then the persisted signature is a 64-char hex HMAC")]
    public async Task Upsert_KnownTheme_PersistsValidHexSignatureAsync()
    {
        var service = BuildService();
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var row = await service.UpsertAsync(orgId, "synthwave-night-dark", actorId);

        row.ManifestSignature.ShouldNotBeNullOrWhiteSpace();
        row.ManifestSignature.Length.ShouldBe(64);
        row.ManifestSignature.ShouldMatch(@"^[0-9a-f]{64}$");
    }

    /// <summary>Given an unknown theme id, when UpsertAsync runs,
    /// then an UnknownThemeException is thrown (controller maps
    /// to 404).</summary>
    [Fact(DisplayName = "Given an unknown themeId, when UpsertAsync runs, then UnknownThemeException is thrown")]
    public async Task Upsert_UnknownTheme_ThrowsAsync()
    {
        var service = BuildService();
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var exception = await Should.ThrowAsync<UnknownThemeException>(
            () => service.UpsertAsync(orgId, "not-a-real-theme", actorId));

        exception.ThemeId.ShouldBe("not-a-real-theme");
    }

    /// <summary>Given an existing installation, when DeleteAsync
    /// runs, then the row is removed and subsequent reads return
    /// null.</summary>
    [Fact(DisplayName = "Given an existing installation, when DeleteAsync runs, then the row is removed")]
    public async Task Delete_RemovesRowAsync()
    {
        var service = BuildService();
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await service.UpsertAsync(orgId, "synthwave-night-dark", actorId);

        var removed = await service.DeleteAsync(orgId);

        removed.ShouldBeTrue();
        var readBack = await service.GetForOrgAsync(orgId);
        readBack.ShouldBeNull();
    }

    /// <summary>Given no existing installation, when DeleteAsync
    /// runs, then the call returns false (idempotent).</summary>
    [Fact(DisplayName = "Given no existing installation, when DeleteAsync runs, then returns false")]
    public async Task Delete_MissingRow_ReturnsFalseAsync()
    {
        var service = BuildService();

        var removed = await service.DeleteAsync(Guid.NewGuid());

        removed.ShouldBeFalse();
    }
}

/// <summary>
///     Build the bundled community-theme registry for tests —
///     mirror of the production
///     <c>BrandingInfrastructureInstaller</c> builder so the
///     tests cover the same data.
/// </summary>
internal static class CommunityThemeRegistryBuilderForTests
{
    public static CommunityThemeRegistry Build()
    {
        return new CommunityThemeRegistry(new Dictionary<string, CommunityTheme>(StringComparer.Ordinal)
        {
            ["synthwave-night-dark"] = new(
                Id: "synthwave-night-dark",
                Name: "Synthwave Night — Dark",
                Version: "0.1.0",
                Author: "plexor-themes"),
            ["synthwave-night-light"] = new(
                Id: "synthwave-night-light",
                Name: "Synthwave Night — Light",
                Version: "0.1.0",
                Author: "plexor-themes"),
            ["paper-light"] = new(
                Id: "paper-light",
                Name: "Paper Light",
                Version: "0.1.0",
                Author: "plexor-themes"),
        });
    }
}

/// <summary>
///     In-memory <see cref="IDataProtectionProvider" /> for unit
///     tests — produces a deterministic key per purpose so the
///     sign-then-verify round trip works in isolation. The
///     production provider persists keys to disk; the test
///     provider is transient by design.
/// </summary>
file static class TestDataProtectionProviderFactory
{
    public static IDataProtectionProvider Create()
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("plexor-host-test");
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }
}
