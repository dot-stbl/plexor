// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertThemeInstallationRequestValidator — FluentValidation chain
// for the PUT /api/v1/branding/theme body. Structural rule only:
// a non-empty ThemeId. The registry membership + signature
// generation happen downstream in the controller (host signs
// from the bundled CommunityThemeRegistry).
// ============================================================================

using FluentValidation;
using Plexor.Modules.Branding.Api.Models.Requests;

namespace Plexor.Modules.Branding.Api.Validation;

/// <summary>
///     Validates the marketplace theme-install body. The
///     <c>ThemeId</c> must be non-empty + match the marketplace id
///     convention. The host-side
///     <c>CommunityThemeRegistry.TryFind</c> resolves whether
///     the id is published; unknown ids surface as
///     <see cref="Plexor.Modules.Branding.Application.Branding.UnknownThemeException" />
///     (mapped to 404) at the controller.
/// </summary>
public sealed class UpsertThemeInstallationRequestValidator
    : AbstractValidator<UpsertThemeInstallationRequest>
{
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
    }
}
