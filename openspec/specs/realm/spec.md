# Capability: realm

## Purpose

Define the Organization / Team / Folder hierarchy that Plexor uses to
scope every resource. Realm is the outermost authentication and
billing boundary; teams and folders are progressively narrower
visibility levels for IAM aggregation and resource isolation.

Plexor ships a single-tenant MVP today but the data model is
multi-tenant from day one — a future SaaS deployment can host many
organizations without re-architecting the schema. The 3-tier nullable
scope (Org → Team → Folder) is the contract every other module
references when it stores a resource.

This capability is owned by `Plexor.Modules.Realm` (formerly named
`Plexor.Modules.Organizations` per the mapping table in root
`AGENTS.md`; rename is in progress, see `openspec/AGENTS.md`).
Schema name in SQL and migrations: `realm`. C# concept names:
`Organization`, `Team`, `Folder`.

## Requirements

### Requirement: Organization is the top-level boundary

The system SHALL treat one `Organization` row as the top-level
authentication and billing boundary. All other entities (users,
clusters, compute resources) SHALL reference an `Organization.Id`
either directly via FK or indirectly via a `Team` / `Folder` FK that
recurses back to `Organization.Id`.

Cross-organization access SHALL be impossible at the database layer
(every query either filters by `Organization.Id` or runs through a
`WHERE org_id = current_setting('plexor.current_org_id')` filter).
Multi-org deployments are isolated: a user authenticated to Org X
SHALL NOT see, mutate, or be billed for resources in Org Y.

A self-hosted single-tenant deploy has exactly one `Organization` row;
a SaaS multi-tenant deploy has many. Both shapes MUST work with the
same code.

### Requirement: Team is IAM aggregation

The system SHALL model teams as `Team` rows under an `Organization`,
typically containing 5–15 members. A team's purpose is IAM
aggregation — multiple users in the same team can be granted shared
role bindings.

In v0.1 the `Team` entity exists and `RoleBinding.TeamId` is
writable, but team-scoped REST endpoints are not yet exposed
(they land in Phase 2). The data shape MUST be ready for the Phase 2
endpoints so the migration does not require a data backfill.

### Requirement: Folder is the default resource scope

The system SHALL model folders as `Folder` rows under a team
(or org-level when `TeamId = null`). Folders are the default
namespace for resources: VMs, app instances, and most other
resources MUST live in a folder unless the operator explicitly opts
into team- or org-level visibility for a shared resource.

A team SHOULD contain multiple folders for separation of concerns
(dev, staging, prod, project-alpha, project-beta). Folder
uniqueness for `(OrgId, TeamId, Slug)` is enforced by the DB.

### Requirement: 3-tier nullable scope

Every scopeable resource SHALL store a 3-tier nullable scope:

| Tier | Visible to | Set when |
|------|-----------|----------|
| Org-level | Everyone in the organization | `OrgId` set, `TeamId = null`, `FolderId = null` |
| Team-level | Everyone in the team | `OrgId` set, `TeamId` set, `FolderId = null` |
| Folder-level | Everyone in the folder | `OrgId` set, `TeamId` set, `FolderId` set |

Resources SHALL default to folder-level. Org-level and team-level
visibility SHALL be opt-in and SHALL require an audit-logged
admin action.

Visibility queries SHALL resolve as follows: a resource with
`FolderId = X` is visible to any user whose role bindings include
`FolderId = X` (or wider: `TeamId` or `OrgId` covering that folder).

### Requirement: UUID v7 identifiers

Every Realm entity (`Organization`, `Team`, `Folder`) SHALL use
UUID v7 (`System.Guid`) for its primary key. UUID v7 is
time-sortable, so range queries on `Id` give insertion order
without a separate `created_at` index on the surrogate key.

The `Id` MUST be assigned at creation by the application layer
(never database `SERIAL` / `IDENTITY`); the migrator seeders MUST
emit `Guid.CreateVersion7()` or its EF Core equivalent when
inserting built-in rows.

### Requirement: Slug uniqueness

Every Realm entity SHALL expose a `Slug` column of URL-safe
lowercase kebab-case identifier (`stbl`, `cloud-hybrid`,
`team-zero`, `project-alpha`). The slug MUST match
`^[a-z0-9-]{1,64}$`.

`Organization.Slug` MUST be unique globally (two orgs cannot share
a slug because the org slug resolves the login request to a tenant
before password verification).
`Team.Slug` MUST be unique per `Organization`.
`Folder.Slug` MUST be unique per `(Organization, Team)` — org-level
folders (`TeamId = null`) MUST be unique within the org.

### Requirement: Status field

Every Realm entity SHALL expose a `Status` column with the allowed
values `"active"` (default) and `"suspended"`. Suspended
entities SHALL remain in the database (no hard delete) and SHALL
refuse new write operations through their respective controllers.
The `Status` MUST be denormalized — no JOIN to a separate status
table.

A `"archived"` value is reserved for Phase 2 and SHALL NOT be
emitted by v0.1 code.

### Requirement: CreatedAt timestamp

Every Realm entity SHALL expose a `CreatedAt` column of type
`DateTimeOffset` (UTC), populated at insertion by the application
layer from the injected `TimeProvider`. The entity MUST satisfy
`Plexor.Shared.Kernel.Common.ICreatedAt` so the global audit and
filtering infrastructure can rely on a uniform contract.

### Requirement: Migration order

`realm` migrations SHALL be applied before every other schema's
migrations. The `Plexor.Migrator` CLI orders schemas by FK
dependency: `realm` → `sigil` → `atlas` → (future) `ledger`,
`forge`, `outpost`, `shard`. A migration that references
`realm.organizations.id` (or `realm.teams.id` / `realm.folders.id`)
from another schema SHALL fail to apply if the `realm` migration
has not run first.

## Key Entities

### `Organization`

`Plexor.Modules.Realm.Domain.Entities.Organization`
(schema `realm.organizations`). Fields:

- `Id : Guid` — UUID v7, PK.
- `Name : string` — display name shown in UI + audit entries.
- `Slug : string` — URL-safe lowercase kebab-case, globally unique.
- `Status : string` — `"active"` (default) or `"suspended"`.
- `CreatedAt : DateTimeOffset` — UTC creation time.

A single self-hosted deploy has one row. A SaaS deploy has many.
The Migrator seeds the first organization on first deploy using
the operator's chosen slug from `plx init`.

### `Team`

`Plexor.Modules.Realm.Domain.Entities.Team`
(schema `realm.teams`). Fields:

- `Id : Guid` — UUID v7, PK.
- `OrgId : Guid` — FK to `realm.organizations.id`.
- `Name : string` — display name (`team-zero`, `platform-core`).
- `Slug : string` — URL-safe lowercase kebab-case, unique per org.
- `Status : string` — `"active"` (default) or `"suspended"`.
- `CreatedAt : DateTimeOffset` — UTC creation time.

`UNIQUE (OrgId, Slug)`. 5–15 members is a typical size; the
schema does not enforce this.

### `Folder`

`Plexor.Modules.Realm.Domain.Entities.Folder`
(schema `realm.folders`). Fields:

- `Id : Guid` — UUID v7, PK.
- `OrgId : Guid` — FK to `realm.organizations.id` (denormalized
  for org-scoped queries; every folder has an org, even
  org-level ones with no team).
- `TeamId : Guid?` — FK to `realm.teams.id`. `null` = org-level
  folder shared across teams.
- `Name : string` — display name (`project-alpha`, `prod`, `dev`).
- `Slug : string` — URL-safe lowercase kebab-case, unique per
  `(OrgId, TeamId)`.
- `Status : string` — `"active"` (default) or `"suspended"`.
- `CreatedAt : DateTimeOffset` — UTC creation time.

`UNIQUE (OrgId, TeamId, Slug)`.