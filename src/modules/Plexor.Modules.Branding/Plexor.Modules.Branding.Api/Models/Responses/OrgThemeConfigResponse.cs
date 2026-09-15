// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgThemeConfigResponse — wire shape for GET /api/v1/branding/org/{orgId}
// + the same shape returned by PUT. Init-property class.
// ============================================================================

namespace Plexor.Modules.Branding.Api.Models.Responses;

/// <summary>
///     Wire shape for the per-org branding override row. Null fields
///     mean "inherit the operator global default" — the frontend
///     merges the two rows at boot time via IBrandingService.ResolveForOrgAsync.
/// </summary>
public sealed class OrgThemeConfigResponse
{
    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Per-org theme preset override. Null = inherit.</summary>
    public string? PresetId { get; init; }

    /// <summary>Per-org custom accent (OKLCH). Null = inherit.</summary>
    public string? CustomAccent { get; init; }

    /// <summary>Per-org brand name. Null = inherit.</summary>
    public string? BrandName { get; init; }

    /// <summary>Per-org logo URL. Null = inherit.</summary>
    public string? BrandLogoUrl { get; init; }

    /// <summary>Per-org favicon URL. Null = inherit.</summary>
    public string? BrandFaviconUrl { get; init; }

    /// <summary>Last update time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
