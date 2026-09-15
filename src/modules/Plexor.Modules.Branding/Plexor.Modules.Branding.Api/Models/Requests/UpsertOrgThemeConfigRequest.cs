// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertOrgThemeConfigRequest — wire shape for PUT /api/v1/branding/org/{orgId}.
// ============================================================================

namespace Plexor.Modules.Branding.Api.Models.Requests;

/// <summary>
///     Wire shape for the per-org branding override upsert body.
///     All fields except <c>OrgId</c> are nullable — a null value
///     means "inherit the operator global default". The path
///     parameter is the authoritative orgId; the body's
///     <see cref="OrgId" /> is ignored.
/// </summary>
public sealed class UpsertOrgThemeConfigRequest
{
    /// <summary>Tenant scope — copied from the route; ignored when
    /// the caller passes a different value (path is authoritative).</summary>
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
}
