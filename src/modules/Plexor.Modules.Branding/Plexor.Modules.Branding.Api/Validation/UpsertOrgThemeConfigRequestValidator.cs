// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertOrgThemeConfigRequestValidator — FluentValidation chain for
// PUT /api/v1/branding/org/{orgId} body. Same rules as the global
// upsert, but every field except OrgId is optional (null = inherit).
// ============================================================================

using FluentValidation;
using Plexor.Modules.Branding.Api.Models.Requests;

namespace Plexor.Modules.Branding.Api.Validation;

/// <summary>
///     Validates the per-org branding override upsert body. Same
///     shape + limits as <see cref="UpsertGlobalThemeConfigRequestValidator" />;
///     every field except <c>OrgId</c> is optional (null = inherit
///     the operator global default).
/// </summary>
public sealed class UpsertOrgThemeConfigRequestValidator
    : AbstractValidator<UpsertOrgThemeConfigRequest>
{
    /// <summary>Construct the validator with the standard rule chain.</summary>
    public UpsertOrgThemeConfigRequestValidator()
    {
        RuleFor(static request => request.OrgId)
            .NotEqual(Guid.Empty)
                .WithMessage("OrgId must not be empty.");

        RuleFor(static request => request.BrandName!)
            .MaximumLength(128)
                .WithMessage("BrandName must be 128 characters or fewer.")
                .When(static request => request.BrandName is not null);

        RuleFor(static request => request.BrandLogoUrl!)
            .MaximumLength(2048)
                .WithMessage("BrandLogoUrl must be 2048 characters or fewer.")
                .When(static request => request.BrandLogoUrl is not null)
            .Must(static url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("BrandLogoUrl must be a valid absolute URL when present.")
                .When(static request => request.BrandLogoUrl is not null);

        RuleFor(static request => request.BrandFaviconUrl!)
            .MaximumLength(2048)
                .WithMessage("BrandFaviconUrl must be 2048 characters or fewer.")
                .When(static request => request.BrandFaviconUrl is not null)
            .Must(static url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("BrandFaviconUrl must be a valid absolute URL when present.")
                .When(static request => request.BrandFaviconUrl is not null);

        RuleFor(static request => request.PresetId!)
            .MaximumLength(64)
                .WithMessage("PresetId must be 64 characters or fewer.")
                .When(static request => request.PresetId is not null)
            .Matches(UpsertGlobalThemeConfigRequestValidator.PresetIdPattern)
                .WithMessage("PresetId must match 'plexor-<kebab-case>'.")
                .When(static request => request.PresetId is not null);

        RuleFor(static request => request.CustomAccent!)
            .MaximumLength(64)
                .WithMessage("CustomAccent must be 64 characters or fewer.")
                .When(static request => request.CustomAccent is not null)
            .Must(static accent => string.IsNullOrEmpty(accent) || System.Text.RegularExpressions.Regex.IsMatch(accent, UpsertGlobalThemeConfigRequestValidator.OklchPattern))
                .WithMessage("CustomAccent must be a valid oklch(...) literal when present.")
                .When(static request => request.CustomAccent is not null);
    }
}