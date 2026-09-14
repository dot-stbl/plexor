// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// GlobalThemeConfigResponse — wire shape for the operator-global
// branding row returned by GET /api/v1/branding/global + the same
// shape returned by PUT (200 + updated entity). Init-property class
// (per anti-patterns.md §2 — no positional records on wire shapes).
// ============================================================================

namespace Plexor.Modules.Branding.Api.Models;

/// <summary>
///     Wire shape for the operator-global branding row.
/// </summary>
public sealed class GlobalThemeConfigResponse
{
    /// <summary>Operator-set product name.</summary>
    public string BrandName { get; init; } = "Plexor";

    /// <summary>URL or web-root-relative path of the logo. Null means
    /// "use the default SVG mark".</summary>
    public string? BrandLogoUrl { get; init; }

    /// <summary>URL or web-root-relative path of the favicon. Null
    /// means "use the default favicon".</summary>
    public string? BrandFaviconUrl { get; init; }

    /// <summary>Stable id of the default frontend theme preset.</summary>
    public string DefaultPresetId { get; init; } = "plexor-default-light";

    /// <summary>Custom accent (OKLCH). Null means "use preset default".</summary>
    public string? CustomAccent { get; init; }

    /// <summary>Last update time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}