// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertGlobalThemeConfigRequest — wire shape for PUT /api/v1/branding/global.
// ============================================================================

namespace Plexor.Modules.Branding.Api.Models.Requests;

/// <summary>
///     Wire shape for the operator-global branding upsert body.
///     Validated by <see cref="Plexor.Modules.Branding.Api.Validation.UpsertGlobalThemeConfigRequestValidator" />.
/// </summary>
public sealed class UpsertGlobalThemeConfigRequest
{
    /// <summary>Operator-set product name (1-128 chars).</summary>
    public string BrandName { get; init; } = "Plexor";

    /// <summary>URL or web-root-relative path of the logo (optional,
    /// ≤2048 chars).</summary>
    public string? BrandLogoUrl { get; init; }

    /// <summary>URL or web-root-relative path of the favicon (optional,
    /// ≤2048 chars).</summary>
    public string? BrandFaviconUrl { get; init; }

    /// <summary>Stable id of the default frontend theme preset. The
    /// backend treats this as an opaque string and validates the
    /// pattern; the frontend's preset registry owns the canonical
    /// list (see web/apps/console/src/shared/lib/themes/presets.ts).</summary>
    public string DefaultPresetId { get; init; } = "plexor-default-light";

    /// <summary>Custom accent (OKLCH string). Optional.</summary>
    public string? CustomAccent { get; init; }
}
