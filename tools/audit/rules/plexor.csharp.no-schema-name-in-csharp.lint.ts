/**
 * plexor.no-schema-name-in-csharp — project AST rule for Plexor.
 *
 * Mirrors AGENTS.md section 1 (Naming — architecture theme vs C# concept).
 *
 * Plexor has two parallel naming systems:
 *   1. Architecture theme: single-word schema names (sigil, realm, forge,
 *      atlas, ledger, outpost, shard). Used for PostgreSQL schemas, project
 *      names, and folders.
 *   2. C# concept: domain words (Organization, Team, Folder, User, Cluster,
 *      Workload, AuditEntry).
 *
 * A C# class declaration MUST NOT take its name from the schema-word
 * vocabulary. Conflating the two breaks DB-schema/ENTITY-name alignment
 * and confuses search (no semantic distinction between schema name and
 * class name).
 *
 * AST: matches a class_declaration whose name field is a bare schema word.
 * The regex anchors on ^(Word)$ so identifiers containing the substring
 * (RealmSummary, ForgeryDetector) do NOT match.
 *
 * Catches:
 *   public sealed class Realm { ... }
 *   public sealed record Forge(...)
 *
 * Does NOT catch:
 *   namespace Plexor.Modules.Sigil (namespace, not class_declaration)
 *   public sealed class RealmEntity (Realm is not the full name)
 *   public sealed class ClusterRealm (Realm is not the full name)
 *   [Table("sigil", "users")] (attribute argument, not class name)
 *   public string RealmPrefix { get; } (field/property, not class)
 *
 * Severity: warning (forward-only documentation-as-code rule; pre-existing
 * legitimate class names left alone until natural refactor).
 */
export default {
  id: 'plexor.no-schema-name-in-csharp',
  language: 'csharp',
  severity: 'warning',
  globs: ['**/*.cs'],
  excludePaths: [
    '**/bin/**',
    '**/obj/**',
    '**/Migrations/**',
    '**/*.Designer.cs',
    '**/*.g.cs',
    '**/*.AssemblyAttributes.cs',
    '**/Generated/**',
  ],
  ast: {
    rule: {
      kind: 'class_declaration',
      has: {
        field: 'name',
        regex:
          '^(Sigil|Realm|Forge|Atlas|Ledger|Outpost|Shard|Identity|Imprint|Beacon|Vault|Tessera)$',
      },
    },
  },
  message:
    'C# class names live in the concept space (Organization, Team, Cluster, Workload, User). Schema words (Sigil, Realm, Forge, Atlas, Ledger, Outpost, Shard) are reserved for PostgreSQL schema names and module project names — never C# class names. See AGENTS.md section 1.',
  source: 'AGENTS.md#1-naming',
  rationale:
    'Two parallel naming systems are load-bearing in Plexor. Schema names appear in CREATE SCHEMA, in DatabaseInformation.Schemes, in project folder names, and in EF migration directories. C# class names appear in user-facing APIs, domain events, error messages, and OpenAPI. Conflating them means a search for "Realm" returns both "Realm PostgreSQL schema" and "Realm C# class" with no semantic distinction. The rule fires on class_declaration AST kind (PascalCase identifier in the type-declaration position) but not on namespaces, fields, properties, or attribute arguments — those are legitimate uses.',
};
