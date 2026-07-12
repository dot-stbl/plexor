namespace Plexor.Shared.Persistence;

/// <summary>
///     Single source of truth for PostgreSQL schema and table names. Module
///     DbContexts use the schema constants to scope their entities; table
///     constants are populated per module as entities are introduced.
/// </summary>
/// <remarks>
///     <para>
///         <b>Schema-per-module.</b> Plexor runs a single PostgreSQL cluster
///         with multiple schemas (one per module + one per cross-cutting
///         concern). The schema naming follows the Architecture theme
///         recorded in <c>.agents/STATE.md</c> — sigil/realm/atlas/etc. —
///         so every schema is named with the same theme-word pattern.
///     </para>
///     <para>
///         <b>Why constants and not strings.</b> EF Core migrations emit
///         DDL from <c>ToTable(...)</c> + <c>HasColumnName(...)</c> calls.
///         A typo in a literal produces a migration that diverges from the
///         entity code (one says <c>tenant_id</c>, the other says
///         <c>teannt_id</c>, the migration silently succeeds). Constants
///         make typos compile-time errors.
///     </para>
///     <para>
///         <b>Module → schema mapping.</b>
///         <list type="table">
///             <listheader>
///                 <term>Schema</term><description>Module</description>
///             </listheader>
///             <item>
///                 <term><see cref="Schemes.Identity" /> (sigil)</term><description>Plexor.Modules.Sigil</description>
///             </item>
///             <item>
///                 <term><see cref="Schemes.Realm" /> (realm)</term><description>Plexor.Modules.Realm</description>
///             </item>
///             <item>
///                 <term><see cref="Schemes.Billing" /> (ledger)</term><description>Plexor.Modules.Billing</description>
///             </item>
///             <item>
///                 <term><see cref="Schemes.Audit" /> (atlas)</term><description>Plexor.Modules.Audit</description>
///             </item>
///             <item>
///                 <term><see cref="Schemes.Clusters" /> (forge)</term>
///                 <description>cross-cutting cluster fleet metadata (planned)</description>
///             </item>
///             <item>
///                 <term><see cref="Schemes.Nodes" /> (outpost)</term>
///                 <description>cross-cutting node registration metadata (planned)</description>
///             </item>
///             <item>
///                 <term><see cref="Schemes.Workloads" /> (shard)</term>
///                 <description>cross-cutting workload catalogue (planned)</description>
///             </item>
///         </list>
///     </para>
/// </remarks>
public static class DatabaseInformation
{
    /// <summary>
    ///     PostgreSQL schema names — one per module. Derived from the
    ///     Architecture theme (see <c>.agents/STATE.md</c> infrastructure
    ///     decisions) so all schemas follow the same one-word convention.
    /// </summary>
    public static class Schemes
    {
        /// <summary>Identity / auth / users (Plexor.Modules.Sigil).</summary>
        public const string Identity = "sigil";

        /// <summary>Organizations / org-team-folder hierarchy (Plexor.Modules.Realm). Note: schema is named <c>realm</c> per the architecture theme; the C# module name is the concept (<c>Organization</c>). See AGENTS.md for the schema-vs-concept map.</summary>
        public const string Realm = "realm";

        /// <summary>Billing / wallets / invoice lines (Plexor.Modules.Billing).</summary>
        public const string Billing = "ledger";

        /// <summary>Audit / event log / immutable append-only (Plexor.Modules.Audit).</summary>
        public const string Audit = "atlas";

        /// <summary>Cluster fleet metadata (cross-cutting, planned).</summary>
        public const string Clusters = "forge";

        /// <summary>Node registration / heartbeat (cross-cutting, planned).</summary>
        public const string Nodes = "outpost";

        /// <summary>Workload catalogue (cross-cutting, planned).</summary>
        public const string Workloads = "shard";
    }

    /// <summary>
    ///     PostgreSQL table names — populated per module as entities are
    ///     added. <see cref="Schemes" /> for schema pairing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Empty in v0.1 — modules will populate this as they introduce
    ///         their first DbContext. Adding a new table = adding a new
    ///         <c>public const string Xxx = "xxx";</c> here, then referencing
    ///         it from the entity's <c>ToTable(...)</c>.
    ///     </para>
    /// </remarks>
    public static class Tables
    {
        /// <summary>Audit module — one row per audited action.</summary>
        public const string AuditEntries = "audit_entries";

        /// <summary>Organizations module — one row per organization (org-scoped resources FK here).</summary>
        public const string Organizations = "organizations";

        /// <summary>Organizations module — team rows belong to a single org.</summary>
        public const string Teams = "teams";

        /// <summary>Organizations module — folder rows belong to an org (team optional for org-level folders).</summary>
        public const string Folders = "folders";

        /// <summary>Identity module — user accounts.</summary>
        public const string Users = "users";

        /// <summary>Identity module — named permission sets.</summary>
        public const string Roles = "roles";

        /// <summary>Identity module — user-to-role attachments, optionally project-scoped.</summary>
        public const string RoleBindings = "role_bindings";

        /// <summary>Identity module — single-use refresh tokens with rotation chains.</summary>
        public const string RefreshTokens = "refresh_tokens";

        /// <summary>Identity module — service-to-service credentials.</summary>
        public const string ApiKeys = "api_keys";

        /// <summary>Identity module — OpenSSH public keys registered per user.</summary>
        public const string SshKeys = "ssh_keys";

        /// <summary>Identity module — JWT signing keypairs (RS256).</summary>
        public const string SigningKeys = "signing_keys";
    }
}
