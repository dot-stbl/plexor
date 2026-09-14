// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ResolvedBrandingConfig — the merged "what the frontend should
// render" snapshot. Built by IBrandingService.ResolveForOrgAsync
// from the operator-global row + the per-org override row. Null
// fields mean "inherit the global default" — the API response uses
// the same null-equals-inherit convention as the underlying DB
// rows.
// ============================================================================

namespace Plexor.Modules.Branding.Application.Branding;

/// <summary>
///     Resolved branding snapshot returned by
///     <see cref="IBrandingService.ResolveForOrgAsync" />. Field values
///     are the merged result of the operator-global GlobalThemeConfig
///     row + the per-org OrgThemeConfig override row. The boot-config
///     script on the FE reads this and writes it to
///     <c>window.__PLEXOR_CONFIG__</c>.
/// </summary>
public sealed class ResolvedBrandingConfig
{
    /// <summary>Resolved brand name (org override wins when non-null).</summary>
    public string BrandName { get; init; } = "Plexor";

    /// <summary>Resolved logo URL — null means "use the default SVG mark".</summary>
    public string? BrandLogoUrl { get; init; }

    /// <summary>Resolved favicon URL — null means "use the default favicon".</summary>
    public string? BrandFaviconUrl { get; init; }

    /// <summary>Resolved theme preset id (always non-null —
    /// the global DefaultPresetId column is NOT NULL in the schema).</summary>
    public string DefaultPresetId { get; init; } = "plexor-default-light";

    /// <summary>Resolved custom accent — null means "use the preset's built-in accent".</summary>
    public string? CustomAccent { get; init; }
}