namespace Plexor.Modules.Quotas.Infrastructure.Persistence;

/// <summary>
///     Module-local single source of truth for the Quotas PostgreSQL
///     schema and table names. The schema name follows the Plexor
///     architecture theme (one-word one-token; <c>quotas</c>); the
///     table names follow snake_case. No literal table or schema
///     strings appear in the entity configurations — they all
///     reference the constants here so a typo becomes a compile-time
///     error.
/// </summary>
/// <remarks>
///     <para><b>Why module-local (not <c>Plexor.Shared.Persistence.DatabaseInformation</c>).</b>
///     The shared class aggregates every schema across the fleet so
///     cross-module FKs can be written down. The Quotas schema is
///     isolated — no other module FKs into <c>quotas.*</c> — so the
///     schema name lives next to the entities that own it.</para>
/// </remarks>
public static class DatabaseInformation
{
    /// <summary>PostgreSQL schema name for the Quotas module.</summary>
    public static class Schemes
    {
        /// <summary>Quota catalog + assignments + usage + rate-limit
        /// events (Plexor.Modules.Quotas).</summary>
        public const string Quotas = "quotas";
    }

    /// <summary>PostgreSQL table names owned by the Quotas module.</summary>
    public static class Tables
    {
        /// <summary>Catalog of limit keys (stable identifiers like
        /// <c>"compute.vms.count"</c>).</summary>
        public const string QuotaDefinitions = "quota_definitions";

        /// <summary>Polymorphic scope → value bindings.</summary>
        public const string QuotaAssignments = "quota_assignments";

        /// <summary>Current consumption snapshot per (scope, definition).</summary>
        public const string QuotaUsage = "quota_usage";

        /// <summary>Append-only rate-limit event log.</summary>
        public const string RateLimitEvents = "rate_limit_events";
    }
}
