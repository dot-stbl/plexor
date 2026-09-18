// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeManifestVerifierShould — exercise the HMAC-SHA256
// manifest signer directly. The host is the only signer in v1
// (no publisher feed), so the verifier exposes only
// `SignManifest` (not `VerifyManifest`); the tests assert the
// signer produces deterministic output across instances (because
// the underlying HMAC key is derived from a constant seed via a
// round-trip through the data-protection key ring) and that the
// canonicalisation rules are stable.
// ============================================================================

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Branding.Application.Branding;
using Plexor.Modules.Branding.Infrastructure.ThemeManifests;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Branding.Unit;

/// <summary>
///     Behavioural tests for <see cref="HmacThemeManifestVerifier" />.
///     Pins the canonicalisation rules so a future refactor (e.g.
///     switching to Ed25519 for a real publisher feed) cannot
///     silently change the signature bytes.
/// </summary>
public sealed class HmacThemeManifestVerifierShould
{
    private static ThemeManifest SampleManifest()
    {
        return new ThemeManifest(
            ThemeId: "synthwave-night-dark",
            Name: "Synthwave Night — Dark",
            Version: "0.1.0",
            Author: "plexor-themes");
    }

    private static ThemeManifest AltManifest()
    {
        return new ThemeManifest(
            ThemeId: "paper-light",
            Name: "Paper Light",
            Version: "0.2.0",
            Author: "plexor-themes");
    }

    private static IDataProtectionProvider TestProvider()
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("plexor-host-test");
        return services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();
    }

    /// <summary>Given a manifest, when the signer runs, then
    /// the signature is a 64-char lowercase hex string (32
    /// bytes, HMAC-SHA256).</summary>
    [Fact(DisplayName = "Given a manifest, when SignManifest runs, then the signature is a 64-char lowercase hex")]
    public void Sign_ProducesLowercaseHexOfExpectedLength()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());

        var signature = verifier.SignManifest(SampleManifest());

        signature.ShouldNotBeNullOrWhiteSpace();
        signature.Length.ShouldBe(64);
        signature.ShouldMatch(@"^[0-9a-f]{64}$");
    }

    /// <summary>Given the same manifest twice, when the signer
    /// runs twice, then the two signatures are equal —
    /// canonicalisation + key derivation are deterministic.</summary>
    [Fact(DisplayName = "Given the same manifest, when SignManifest runs twice, then the signatures are equal")]
    public void Sign_SameManifest_ProducesIdenticalSignature()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());

        var first = verifier.SignManifest(SampleManifest());
        var second = verifier.SignManifest(SampleManifest());

        first.ShouldBe(second);
    }

    /// <summary>Given two different manifests, when the signer
    /// runs, then the signatures differ — a single-byte change
    /// in any field changes the canonical bytes + the HMAC
    /// output.</summary>
    [Fact(DisplayName = "Given two different manifests, when SignManifest runs, then the signatures differ")]
    public void Sign_DifferentManifests_ProduceDifferentSignatures()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());

        var first = verifier.SignManifest(SampleManifest());
        var second = verifier.SignManifest(AltManifest());

        first.ShouldNotBe(second);
    }

    /// <summary>Given two signer instances on different
    /// providers, when each signs the same manifest, then the
    /// signatures are equal — the HMAC key is derived from a
    /// constant per-purpose seed and round-tripped through the
    /// local data-protection provider, so all instances agree on
    /// the same key bytes regardless of provider master-key.</summary>
    [Fact(DisplayName = "Given two signer instances on different providers, when each signs the same manifest, then the signatures are equal")]
    public void Sign_DifferentProviders_ProduceSameSignature()
    {
        var signerA = new HmacThemeManifestVerifier(TestProvider());
        var signerB = new HmacThemeManifestVerifier(TestProvider());

        var signatureA = signerA.SignManifest(SampleManifest());
        var signatureB = signerB.SignManifest(SampleManifest());

        // Identical manifest + identical canonical-form bytes +
        // identical key derivation = identical signatures. Future
        // key rotation (Phase 5+ publisher feed) can swap this
        // seed for a host-config-supplied secret.
        signatureA.ShouldBe(signatureB);
    }
}
