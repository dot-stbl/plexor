// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GlobalThemeConfig — operator-level branding defaults for the entire
// Plexor install. The schema is `branding.global_theme_config` and
// the table holds at most one row (singleton) — created on first
// host startup by BrandingGlobalSeeder.
//
// For SaaS / multi-tenant deploys the operator defaults are the
// fallback when a tenant's OrgThemeConfig row doesn't override a
// given field. See OrgThemeConfig for the per-tenant override shape.
// ============================================================================

namespace Plexor.Modules.Branding.Domain.Entities;

/// <summary>
///     Operator-level branding defaults. Singleton row (the table
///     holds at most one); created by
///     <c>BrandingGlobalSeeder</c> on host startup when the table is
///     empty.
/// </summary>
/// <remarks>
///     <para><b>Sorted row.</b> The seeder runs <c>SELECT WHERE
///     id = (the singleton id)</c> first; if no row exists, inserts
///     one. Idempotent on rerun. UI always treats this row as "the
///     operator defaults" — never as a per-tenant value.</para>
///     <para><b>Why an entity rather than a single-row key/value
///     table.</b> EF Core migrations work best on typed entities;
///     a JSON blob would be opaque to the design-time migration
///     snapshot. The single-row invariant is enforced by a UNIQUE
///     partial index on <c>id IS NOT NULL</c> (Postgres idiom).</para>
///     <para><b>Preset id is the frontend registry id.</b> The
///     <see cref="DefaultPresetId" /> value is one of
///     <c>"plexor-default-light"</c>, <c>"plexor-default-dark"</c>,
///     or <c>"plexor-noir"</c> — the ids in
///     <c>web/apps/console/src/shared/lib/themes/presets.ts</c>.
///     The frontend registry owns the list; the backend treats the
///     value as an opaque string and the frontend validates the
///     choice at save time.</para>
/// </remarks>
public sealed class GlobalThemeConfig
{
    /// <summary>Singleton row id. The seeder always uses this constant
    /// — querying by id makes the singleton invariant explicit at the
    /// call site instead of relying on a "where rownum = 1" idiom.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>Sentinel id (matches <see cref="SingletonId" />).</summary>
    public Guid Id { get; init; } = SingletonId;

    /// <summary>Operator-set product name shown in the sidebar, page
    /// title, and favicon alt text. Defaults to <c>"Plexor"</c> when
    /// the seeder creates the row.</summary>
    public string BrandName { get; init; } = "Plexor";

    /// <summary>URL or web-root-relative path of the brand logo image
    /// (replaces the default SVG mark in the sidebar header). Null
    /// means "use the default SVG mark".</summary>
    public string? BrandLogoUrl { get; init; }

    /// <summary>URL or web-root-relative path of the favicon. Null
    /// means "use the default favicon". The boot script in
    /// <c>main.tsx</c> reads this at boot time.</summary>
    public string? BrandFaviconUrl { get; init; }

    /// <summary>Stable id of the default frontend theme preset.
    /// One of <c>"plexor-default-light"</c>,
    /// <c>"plexor-default-dark"</c>, <c>"plexor-noir"</c>. The seeder
    /// uses <c>"plexor-default-light"</c>.</summary>
    public string DefaultPresetId { get; init; } = "plexor-default-light";

    /// <summary>Custom accent colour override (OKLCH string,
    /// e.g. <c>"oklch(0.65 0.18 250)"</c>). Null = use the preset's
    /// built-in accent.</summary>
    public string? CustomAccent { get; init; }

    /// <summary>Id of the operator user that last updated the row.
    /// Null when the seeder created it (no human touch).</summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>Last update time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}