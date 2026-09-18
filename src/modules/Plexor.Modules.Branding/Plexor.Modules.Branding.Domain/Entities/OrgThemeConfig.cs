// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgThemeConfig — per-tenant branding override. SaaS / multi-tenant
// deploys let each tenant override the operator defaults shipped in
// GlobalThemeConfig; single-tenant deploys leave the table empty.
//
// Null fields fall back to the matching GlobalThemeConfig field; the
// BrandingController merges the two rows into a single ResolvedBootConfig
// the frontend consumes at boot time. See IBrandingService.ResolveForOrgAsync.
// ============================================================================

namespace Plexor.Modules.Branding.Domain.Entities;

/// <summary>
///     Per-tenant branding override. One row per
///     <c>realm.organizations.id</c>; UNIQUE on <c>OrgId</c>. The
///     <c>branding.org_theme_config</c> table is empty in
///     single-tenant installs.
/// </summary>
/// <remarks>
///     <para><b>Null = inherit.</b> Every field except
///     <see cref="OrgId" /> is nullable — a non-null value overrides
///     the matching <see cref="GlobalThemeConfig" /> field; a null
///     value inherits the operator default. This lets a tenant
///     override only the brand logo without touching the theme
///     preset, etc.</para>
///     <para><b>Delete = full reset.</b> The DELETE /api/v1/branding/org/{orgId}
///     endpoint deletes the row outright (no soft delete) — the
///     tenant fully reverts to the operator defaults. Audit trail
///     is the responsibility of the host's atlas.audit_entries
///     integration in a later phase.</para>
/// </remarks>
public sealed class OrgThemeConfig
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>FK to <c>realm.organizations.id</c>. UNIQUE — one
    /// override row per org.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Theme preset override. Null = inherit
    /// <c>GlobalThemeConfig.DefaultPresetId</c>.</summary>
    public string? PresetId { get; init; }

    /// <summary>Custom accent colour override (OKLCH string).
    /// Null = use the preset's built-in accent.</summary>
    public string? CustomAccent { get; init; }

    /// <summary>Per-tenant brand name override. Null = inherit
    /// <c>GlobalThemeConfig.BrandName</c>.</summary>
    public string? BrandName { get; init; }

    /// <summary>Per-tenant logo override. Null = inherit
    /// <c>GlobalThemeConfig.BrandLogoUrl</c>.</summary>
    public string? BrandLogoUrl { get; init; }

    /// <summary>Per-tenant favicon override. Null = inherit
    /// <c>GlobalThemeConfig.BrandFaviconUrl</c>.</summary>
    public string? BrandFaviconUrl { get; init; }

    /// <summary>Id of the operator user that last updated the row.
    /// Null when first inserted by the migrator.</summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>Last update time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}