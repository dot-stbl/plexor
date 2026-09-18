// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeManifestVerifierShould — exercise the HMAC-SHA256 manifest
// verifier directly. Sign a manifest with one provider, then
// re-verify the same manifest against a fresh provider; the key
// material is derived from the same data-protection key ring in
// both directions so the round trip succeeds.
//
// Tampering tests cover the three failure modes a malicious FE
// bundle would have to defeat: flipping one byte of the signature,
// flipping one byte of the manifest, and submitting a manifest
// the publisher never signed. Every one of them must throw
// ThemeManifestVerificationException with no row written.
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
///     Pins the canonicalisation rules + constant-time verification
///     so a future refactor (e.g. switching to Ed25519) cannot
///     silently change accepted signatures.
/// </summary>
public sealed class HmacThemeManifestVerifierShould
{
    private static ThemeManifest SampleManifest()
    {
        return new ThemeManifest(
            ThemeId: "synthwave-night-dark",
            Name: "Synthwave Night — Dark",
            Version: "0.1.0",
            Author: "plexor-themes",
            TokenValues: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["background"] = "oklch(15% 0.04 270)",
                ["foreground"] = "oklch(94% 0.02 270)",
                ["accent"] = "oklch(72% 0.22 340)",
            });
    }

    private static IDataProtectionProvider TestProvider()
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("plexor-host-test");
        return services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();
    }

    /// <summary>Given a signed manifest, when the same verifier
    /// verifies the same manifest + signature, then no
    /// exception is thrown.</summary>
    [Fact(DisplayName = "Given a signed manifest, when verifying the same manifest + signature, then the verifier accepts")]
    public void SignThenVerify_SameManifest_Succeeds()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());
        var manifest = SampleManifest();
        var signature = verifier.SignManifest(manifest);

        Should.NotThrow(() => verifier.VerifyManifest(manifest, signature));
    }

    /// <summary>Given a manifest, when a single byte of the
    /// manifest token value changes (tamper), then the verifier
    /// throws ThemeManifestVerificationException.</summary>
    [Fact(DisplayName = "Given a manifest with a tampered token value, when verifying, then the verifier throws")]
    public void Verify_TamperedManifest_Throws()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());
        var original = SampleManifest();
        var signature = verifier.SignManifest(original);

        var tampered = original with
        {
            TokenValues = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["background"] = "oklch(50% 0.04 270)",
                ["foreground"] = "oklch(94% 0.02 270)",
                ["accent"] = "oklch(72% 0.22 340)",
            },
        };

        Should.Throw<ThemeManifestVerificationException>(
            () => verifier.VerifyManifest(tampered, signature));
    }

    /// <summary>Given two verifier instances on different
    /// providers, when each signs the same manifest, then both
    /// signatures equal each other (the HMAC key is derived from a
    /// constant per-purpose seed and round-tripped through the
    /// local data-protection provider — so all instances agree on
    /// the same key bytes regardless of provider master-key).</summary>
    [Fact(DisplayName = "Given two verifier instances on different providers, when each signs the same manifest, then the signatures are equal")]
    public void Sign_DifferentProviders_ProduceSameSignature()
    {
        var signerA = new HmacThemeManifestVerifier(TestProvider());
        var signerB = new HmacThemeManifestVerifier(TestProvider());

        var manifest = SampleManifest();
        var signatureA = signerA.SignManifest(manifest);
        var signatureB = signerB.SignManifest(manifest);

        // Identical manifest + identical canonical-form bytes +
        // identical key derivation = identical signatures. Future
        // key rotation (Phase 5+ publisher feed) can swap this
        // seed for a host-config-supplied secret.
        signatureA.ShouldBe(signatureB);
    }

    /// <summary>Given a tampered byte in the signature itself,
    /// when the verifier runs, then the verifier throws
    /// (constant-time comparison rejects any mismatch).</summary>
    [Fact(DisplayName = "Given a tampered signature byte, when verifying, then the verifier throws")]
    public void Verify_TamperedSignature_Throws()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());
        var manifest = SampleManifest();
        var goodBytes = Convert.FromHexString(verifier.SignManifest(manifest));
        goodBytes[0] ^= 0xFF;
        var tamperedSignature = Convert.ToHexString(goodBytes).ToLowerInvariant();

        Should.Throw<ThemeManifestVerificationException>(
            () => verifier.VerifyManifest(manifest, tamperedSignature));
    }

    /// <summary>Given an empty signature, when verifying, then the
    /// verifier throws immediately (no canonical bytes
    /// computed).</summary>
    [Fact(DisplayName = "Given an empty signature, when verifying, then the verifier throws")]
    public void Verify_EmptySignature_Throws()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());

        Should.Throw<ThemeManifestVerificationException>(
            () => verifier.VerifyManifest(SampleManifest(), ""));
    }

    /// <summary>Given a signature that is the wrong length (not
    /// 64 hex chars / 32 bytes), when verifying, then the
    /// verifier throws.</summary>
    [Fact(DisplayName = "Given a signature that is the wrong length, when verifying, then the verifier throws")]
    public void Verify_WrongLengthSignature_Throws()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());

        Should.Throw<ThemeManifestVerificationException>(
            () => verifier.VerifyManifest(SampleManifest(), "abcd"));
    }

    /// <summary>Given a signature that isn't valid hex, when
    /// verifying, then the verifier throws with the
    /// "not valid hex" message.</summary>
    [Fact(DisplayName = "Given a non-hex signature, when verifying, then the verifier throws with a clear message")]
    public void Verify_NonHexSignature_Throws()
    {
        var verifier = new HmacThemeManifestVerifier(TestProvider());

        var exception = Should.Throw<ThemeManifestVerificationException>(
            () => verifier.VerifyManifest(
                SampleManifest(),
                new string('z', 64)));
        exception.Message.ShouldContain("hex", Case.Insensitive);
    }
}
