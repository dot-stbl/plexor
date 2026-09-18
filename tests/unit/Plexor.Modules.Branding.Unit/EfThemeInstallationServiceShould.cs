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
using NSubstitute;
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

    private static ThemeManifest ManifestFor(string themeId = "synthwave-night-dark")
    {
        return new ThemeManifest(
            ThemeId: themeId,
            Name: "Synthwave Night — Dark",
            Version: "0.1.0",
            Author: "plexor-themes",
            TokenValues: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["background"] = "oklch(15% 0.04 270)",
                ["accent"] = "oklch(72% 0.22 340)",
            });
    }

    private static EfThemeInstallationService BuildService(out HmacThemeManifestVerifier verifier)
    {
        var clock = new FakeClock(FixedNow);
        var registry = CommunityThemeRegistryBuilderForTests.Build();
        verifier = new HmacThemeManifestVerifier(TestDataProtectionProviderFactory.Create());
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
        var service = BuildService(out _);

        var row = await service.GetForOrgAsync(Guid.NewGuid());

        row.ShouldBeNull();
    }

    /// <summary>Given an upsert, when GetForOrgAsync runs, then
    /// the upserted row is returned with the supplied signature
    /// and the supplied actor.</summary>
    [Fact(DisplayName = "Given an upsert, when GetForOrgAsync runs, then the row comes back with the supplied signature + actor")]
    public async Task Upsert_ThenGet_RoundTripsAsync()
    {
        var service = BuildService(out var verifier);
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var manifest = ManifestFor();
        var signature = verifier.SignManifest(manifest);

        var upserted = await service.UpsertAsync(orgId, manifest.ThemeId, manifest, signature, actorId);

        upserted.OrgId.ShouldBe(orgId);
        upserted.ThemeId.ShouldBe(manifest.ThemeId);
        upserted.ManifestSignature.ShouldBe(signature);
        upserted.ActivatedBy.ShouldBe(actorId);
        upserted.ActivatedAt.ShouldBe(FixedNow);

        var readBack = await service.GetForOrgAsync(orgId);
        readBack.ShouldNotBeNull();
        readBack.ManifestSignature.ShouldBe(signature);
    }

    /// <summary>Given an existing installation, when UpsertAsync
    /// runs again with a new theme id + new signature, then the
    /// row is updated in place (same row id, new values) — the
    /// orchestrator surfaces the upserted row.</summary>
    [Fact(DisplayName = "Given an existing installation, when UpsertAsync runs with a new themeId, then the row updates in place")]
    public async Task Upsert_ExistingInstallation_UpdatesInPlaceAsync()
    {
        var service = BuildService(out var verifier);
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var firstManifest = ManifestFor("synthwave-night-dark");
        var firstSignature = verifier.SignManifest(firstManifest);

        var first = await service.UpsertAsync(orgId, firstManifest.ThemeId, firstManifest, firstSignature, actorId);

        var secondManifest = ManifestFor("paper-light");
        var secondSignature = verifier.SignManifest(secondManifest);

        var second = await service.UpsertAsync(orgId, secondManifest.ThemeId, secondManifest, secondSignature, actorId);

        second.Id.ShouldBe(first.Id);
        second.ThemeId.ShouldBe("paper-light");
        second.ManifestSignature.ShouldBe(secondSignature);
    }

    /// <summary>Given a tampered signature, when UpsertAsync runs,
    /// then a ThemeManifestVerificationException is thrown and
    /// no row is written to the database.</summary>
    [Fact(DisplayName = "Given a tampered signature, when UpsertAsync runs, then verification throws and no row is written")]
    public async Task Upsert_TamperedSignature_ThrowsAndWritesNothingAsync()
    {
        var service = BuildService(out var verifier);
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var manifest = ManifestFor();
        var goodSignature = verifier.SignManifest(manifest);
        var tamperedBytes = Convert.FromHexString(goodSignature);
        tamperedBytes[0] ^= 0xFF;
        var tamperedSignature = Convert.ToHexString(tamperedBytes).ToLowerInvariant();

        await Should.ThrowAsync<ThemeManifestVerificationException>(
            () => service.UpsertAsync(orgId, manifest.ThemeId, manifest, tamperedSignature, actorId));

        var readBack = await service.GetForOrgAsync(orgId);
        readBack.ShouldBeNull();
    }

    /// <summary>Given an unknown theme id, when UpsertAsync runs,
    /// then an UnknownThemeException is thrown (controller maps
    /// to 404).</summary>
    [Fact(DisplayName = "Given an unknown themeId, when UpsertAsync runs, then UnknownThemeException is thrown")]
    public async Task Upsert_UnknownTheme_ThrowsAsync()
    {
        var service = BuildService(out var verifier);
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var manifest = ManifestFor("not-a-real-theme");
        var signature = verifier.SignManifest(manifest);

        var exception = await Should.ThrowAsync<UnknownThemeException>(
            () => service.UpsertAsync(orgId, manifest.ThemeId, manifest, signature, actorId));

        exception.ThemeId.ShouldBe("not-a-real-theme");
    }

    /// <summary>Given an existing installation, when DeleteAsync
    /// runs, then the row is removed and subsequent reads return
    /// null.</summary>
    [Fact(DisplayName = "Given an existing installation, when DeleteAsync runs, then the row is removed")]
    public async Task Delete_RemovesRowAsync()
    {
        var service = BuildService(out var verifier);
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var manifest = ManifestFor();
        var signature = verifier.SignManifest(manifest);
        await service.UpsertAsync(orgId, manifest.ThemeId, manifest, signature, actorId);

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
        var service = BuildService(out _);

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
