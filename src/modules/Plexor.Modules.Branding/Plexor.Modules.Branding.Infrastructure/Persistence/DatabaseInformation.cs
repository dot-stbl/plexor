// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Module-local DatabaseInformation for Plexor.Modules.Branding. The
// schema name `branding` follows the Plexor architecture theme (one
// word, one token — see AGENTS.md). Lives next to the entities that
// own the schema; the shared Plexor.Shared.Persistence.DatabaseInformation
// is reserved for cross-module aggregation.
// ============================================================================

namespace Plexor.Modules.Branding.Infrastructure.Persistence;

/// <summary>
///     Module-local single source of truth for the Branding
///     PostgreSQL schema + table names. The schema follows the
///     architecture theme (`branding`); tables follow snake_case.
///     Constants are referenced by every entity configuration so a
///     typo becomes a compile-time error.
/// </summary>
public static class DatabaseInformation
{
    /// <summary>PostgreSQL schema names — one one per module.</summary>
    public static class Schemes
    {
        /// <summary>Operator global branding + per-org overrides
        /// (Plexor.Modules.Branding).</summary>
        public const string Branding = "branding";
    }

    /// <summary>PostgreSQL table names owned by the Branding module.</summary>
    public static class Tables
    {
        /// <summary>Singleton operator-level branding row
        /// (<c>branding.global_theme_config</c>).</summary>
        public const string GlobalThemeConfig = "global_theme_config";

        /// <summary>Per-org branding overrides
        /// (<c>branding.org_theme_config</c>) — one row per
        /// <c>realm.organizations.id</c>.</summary>
        public const string OrgThemeConfig = "org_theme_config";
    }
}