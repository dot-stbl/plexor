// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertGlobalThemeConfigRequestValidatorShould — exercise the
// FluentValidation chain in isolation. No DB, no controller — the
// validator is a pure function over the request DTO.
// ============================================================================

using FluentValidation.TestHelper;
using Plexor.Modules.Branding.Api.Models.Requests;
using Plexor.Modules.Branding.Api.Validation;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Branding.Unit;

/// <summary>
///     Behavioural tests for
///     <see cref="UpsertGlobalThemeConfigRequestValidator" />.
///     Covers the rule chain documented on the validator's class
///     remarks: non-empty BrandName, valid OKLCH CustomAccent,
///     valid absolute URLs for the logo + favicon, preset-id pattern.
/// </summary>
public sealed class UpsertGlobalThemeConfigRequestValidatorShould
{
    private static UpsertGlobalThemeConfigRequest BuildRequest(
        string brandName = "Plexor",
        string defaultPresetId = "plexor-default-light",
        string? brandLogoUrl = null,
        string? brandFaviconUrl = null,
        string? customAccent = null)
    {
        return new UpsertGlobalThemeConfigRequest
        {
            BrandName = brandName,
            DefaultPresetId = defaultPresetId,
            BrandLogoUrl = brandLogoUrl,
            BrandFaviconUrl = brandFaviconUrl,
            CustomAccent = customAccent,
        };
    }

    /// <summary>Given every field valid, when the validator runs,
    /// then no errors are reported.</summary>
    [Fact(DisplayName = "Given all valid fields, when ValidateAsync runs, then no errors are reported")]
    public async Task Validate_WithAllValidFields_PassesAsync()
    {
        var validator = new UpsertGlobalThemeConfigRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(
            brandLogoUrl: "https://acme.example/logo.svg",
            customAccent: "oklch(0.65 0.18 250)"));

        result.IsValid.ShouldBeTrue();
    }

    /// <summary>Given an empty <c>BrandName</c>, when the validator
    /// runs, then the rule fails.</summary>
    [Fact(DisplayName = "Given empty BrandName, when ValidateAsync runs, then BrandName rule fails")]
    public async Task Validate_WithEmptyBrandName_ReturnsErrorAsync()
    {
        var validator = new UpsertGlobalThemeConfigRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(brandName: string.Empty));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.BrandName);
    }

    /// <summary>Given a malformed preset id, when the validator runs,
    /// then the preset-id rule fails.</summary>
    [Fact(DisplayName = "Given unknown DefaultPresetId, when ValidateAsync runs, then DefaultPresetId rule fails")]
    public async Task Validate_WithMalformedPresetId_ReturnsErrorAsync()
    {
        var validator = new UpsertGlobalThemeConfigRequestValidator();

        var result = await validator.TestValidateAsync(
            BuildRequest(defaultPresetId: "Plexor Light"));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.DefaultPresetId);
    }

    /// <summary>Given a malformed OKLCH literal, when the validator
    /// runs, then the custom-accent rule fails.</summary>
    [Fact(DisplayName = "Given malformed CustomAccent, when ValidateAsync runs, then CustomAccent rule fails")]
    public async Task Validate_WithMalformedCustomAccent_ReturnsErrorAsync()
    {
        var validator = new UpsertGlobalThemeConfigRequestValidator();

        var result = await validator.TestValidateAsync(
            BuildRequest(customAccent: "rgb(255, 0, 0)"));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.CustomAccent);
    }

    /// <summary>Given a malformed logo URL, when the validator runs,
    /// then the brand-logo-url rule fails.</summary>
    [Fact(DisplayName = "Given malformed BrandLogoUrl, when ValidateAsync runs, then BrandLogoUrl rule fails")]
    public async Task Validate_WithMalformedLogoUrl_ReturnsErrorAsync()
    {
        var validator = new UpsertGlobalThemeConfigRequestValidator();

        var result = await validator.TestValidateAsync(
            BuildRequest(brandLogoUrl: "not-a-url"));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static r => r.BrandLogoUrl);
    }
}