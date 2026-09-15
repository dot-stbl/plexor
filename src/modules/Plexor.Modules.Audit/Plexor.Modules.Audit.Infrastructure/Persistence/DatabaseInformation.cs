namespace Plexor.Modules.Audit.Infrastructure.Persistence;

/// <summary>
///     Module-local single source of truth for the Audit PostgreSQL
///     schema and table names. The schema name follows the Plexor
///     architecture theme (one-word one-token; <c>atlas</c>); the
///     table names follow snake_case. No literal table or schema
///     strings appear in the entity configurations — they all
///     reference the constants here so a typo becomes a compile-time
///     error.
/// </summary>
/// <remarks>
///     <para><b>Why module-local (not <c>Plexor.Shared.Persistence.DatabaseInformation</c>).</b>
///     The shared class aggregates every schema across the fleet so
///     cross-module FKs can be written down. The Audit schema is
///     isolated — no other module FKs into <c>atlas.*</c> — so the
///     schema name lives next to the entities that own it. (Audit is
///     a fan-in target for FK references from other schemas in
///     theory, but the append-only nature of the table makes a DB-
///     level FK to <c>audit_entries</c> impractical — the row is
///     deleted by retention long before the source row would have a
///     reason to reference it.)</para>
/// </remarks>
public static class DatabaseInformation
{
    /// <summary>PostgreSQL schema name for the Audit module.</summary>
    public static class Schemes
    {
        /// <summary>Append-only audit log (Plexor.Modules.Audit).</summary>
        public const string Audit = "atlas";
    }

    /// <summary>PostgreSQL table names owned by the Audit module.</summary>
    public static class Tables
    {
        /// <summary>One row per audited action — wire-name discriminator
        /// + tenant scope + JSON-encoded context blob.</summary>
        public const string AuditEntries = "audit_entries";
    }
}
