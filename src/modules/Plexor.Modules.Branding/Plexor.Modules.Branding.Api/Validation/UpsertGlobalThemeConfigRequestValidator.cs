// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertGlobalThemeConfigRequestValidator — FluentValidation chain
// for PUT /api/v1/branding/global body. Runs in the controller via
// [FromServices] before any IBrandingService call; failures surface
// as 400 ValidationProblemDetails.
// ============================================================================

using FluentValidation;
using Plexor.Modules.Branding.Api.Models.Requests;

namespace Plexor.Modules.Branding.Api.Validation;

/// <summary>
///     Validates the operator-global branding upsert body. Rules:
///     <list type="bullet">
///       <item><see cref="UpsertGlobalThemeConfigRequest.BrandName" />
///       is non-empty and ≤ 128 characters.</item>
///       <item><see cref="UpsertGlobalThemeConfigRequest.BrandLogoUrl" />
///       when present is a parseable URL, ≤ 2048 chars.</item>
///       <item><see cref="UpsertGlobalThemeConfigRequest.BrandFaviconUrl" />
///       when present is a parseable URL, ≤ 2048 chars.</item>
///       <item><see cref="UpsertGlobalThemeConfigRequest.DefaultPresetId" />
///       is non-empty and matches the preset-id pattern
///       (<c>plexor-...</c>, lowercase kebab-case).</item>
///       <item><see cref="UpsertGlobalThemeConfigRequest.CustomAccent" />
///       when present is a parseable OKLCH string.</item>
///     </list>
/// </summary>
public sealed class UpsertGlobalThemeConfigRequestValidator
    : AbstractValidator<UpsertGlobalThemeConfigRequest>
{
    /// <summary>Pattern the frontend preset registry uses — lowercase
    /// kebab-case with the <c>plexor-</c> prefix. The backend treats
    /// this as opaque but validates the shape so a typo at the
    /// boundary fails fast.</summary>
    public const string PresetIdPattern = "^plexor-[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>Pattern for a parseable OKLCH colour literal.
    /// Matches the production format the frontend ships in
    /// <c>presets.ts</c> — accepts both
    /// <c>oklch(0.65 0.18 250)</c> (0-1 lightness) and
    /// <c>oklch(65% 0.18 250)</c> (0-100% lightness).
    /// The chroma + hue slot uses <c>[^)]+</c> so values like
    /// <c>0.05 250</c>, <c>0.05% 250</c>, or
    /// <c>0.18 250 / 100%</c> (alpha) all parse.</summary>
    public const string OklchPattern = @"^oklch\(\s*(?:100|[0-9]{1,2}(?:\.[0-9]+)?)(?:\s*%)?\s+[^)]+\)$";

    /// <summary>Construct the validator with the standard rule chain.</summary>
    public UpsertGlobalThemeConfigRequestValidator()
    {
        RuleFor(static request => request.BrandName)
            .NotEmpty()
                .WithMessage("BrandName is required.")
            .MaximumLength(128)
                .WithMessage("BrandName must be 128 characters or fewer.");

        RuleFor(static request => request.BrandLogoUrl)
            .MaximumLength(2048)
                .WithMessage("BrandLogoUrl must be 2048 characters or fewer.")
            .Must(static url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("BrandLogoUrl must be a valid absolute URL when present.");

        RuleFor(static request => request.BrandFaviconUrl)
            .MaximumLength(2048)
                .WithMessage("BrandFaviconUrl must be 2048 characters or fewer.")
            .Must(static url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("BrandFaviconUrl must be a valid absolute URL when present.");

        RuleFor(static request => request.DefaultPresetId)
            .NotEmpty()
                .WithMessage("DefaultPresetId is required.")
            .MaximumLength(64)
                .WithMessage("DefaultPresetId must be 64 characters or fewer.")
            .Matches(PresetIdPattern)
                .WithMessage("DefaultPresetId must match 'plexor-<kebab-case>'.");

        RuleFor(static request => request.CustomAccent)
            .MaximumLength(64)
                .WithMessage("CustomAccent must be 64 characters or fewer.")
            .Must(static accent => string.IsNullOrEmpty(accent) || System.Text.RegularExpressions.Regex.IsMatch(accent, OklchPattern))
                .WithMessage("CustomAccent must be a valid oklch(...) literal when present.");
    }
}