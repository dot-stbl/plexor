// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertThemeInstallationRequestValidator — FluentValidation chain
// for the PUT /api/v1/branding/theme body. Structural rules only;
// the manifest signature is verified by
// IThemeManifestVerifier at the service layer, and the themeId
// lookup happens in CommunityThemeRegistry.
// ============================================================================

using FluentValidation;
using Plexor.Modules.Branding.Api.Models.Requests;

namespace Plexor.Modules.Branding.Api.Validation;

/// <summary>
///     Validates the marketplace theme-install body. The
///     <c>ThemeId</c> + <c>Manifest.ThemeId</c> must agree so a
///     publisher can't bind one manifest to a different id; the
///     <c>Signature</c> must be a 64-char lowercase hex (32-byte
///     HMAC-SHA256). The signature value itself is re-verified
///     cryptographically downstream — this validator only checks
///     the structural envelope.
/// </summary>
public sealed class UpsertThemeInstallationRequestValidator
    : AbstractValidator<UpsertThemeInstallationRequest>
{
    /// <summary>Hex string length for an HMAC-SHA256 (32 bytes =
    /// 64 lowercase hex chars).</summary>
    private const int SignatureHexLength = 64;

    /// <summary>Construct the validator with the marketplace
    /// structural rule chain.</summary>
    public UpsertThemeInstallationRequestValidator()
    {
        RuleFor(static request => request.ThemeId)
            .NotEmpty()
                .WithMessage("ThemeId must not be empty.")
            .MaximumLength(64)
                .WithMessage("ThemeId must be 64 characters or fewer.")
            .Matches(UpsertGlobalThemeConfigRequestValidator.PresetIdPattern)
                .WithMessage(
                    "ThemeId must match the marketplace id convention (lowercase kebab-case, no whitespace).");

        RuleFor(static request => request.Manifest.ThemeId)
            .NotEmpty()
                .WithMessage("Manifest.ThemeId must not be empty.")
            .Equal(static request => request.ThemeId)
                .WithMessage(
                    "Manifest.ThemeId must match the request's ThemeId — the publisher bound one manifest to this id and the host doesn't accept a different manifest under the same id.");

        RuleFor(static request => request.Manifest.Name)
            .NotEmpty()
                .WithMessage("Manifest.Name must not be empty.")
            .MaximumLength(128)
                .WithMessage("Manifest.Name must be 128 characters or fewer.");

        RuleFor(static request => request.Manifest.Version)
            .NotEmpty()
                .WithMessage("Manifest.Version must not be empty.")
            .MaximumLength(64)
                .WithMessage("Manifest.Version must be 64 characters or fewer.");

        RuleFor(static request => request.Manifest.Author)
            .NotEmpty()
                .WithMessage("Manifest.Author must not be empty.")
            .MaximumLength(128)
                .WithMessage("Manifest.Author must be 128 characters or fewer.");

        RuleFor(static request => request.Signature)
            .NotEmpty()
                .WithMessage("Signature must not be empty.")
            .Length(SignatureHexLength)
                .WithMessage(
                    $"Signature must be exactly {SignatureHexLength} characters (hex-encoded HMAC-SHA256).")
            // Hex codec accepts upper + lowercase [0-9A-F]. Lowercase
            // is what the verifier emits; we allow both because
            // publishers running on Windows crypto APIs often
            // emit uppercase by default.
            .Matches(@"^[0-9a-fA-F]+$")
                .WithMessage("Signature must be hex (characters [0-9a-fA-F]).");
    }
}
