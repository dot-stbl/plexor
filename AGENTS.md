# AGENTS — Plexor project guide for AI agents and new contributors

> Read this first. It explains the two-name system (architecture theme vs
> C# concept), the resource-scope hierarchy, and the migration order.
> Skipping this is how you end up writing `realm.tenants` next to a class
> called `Organization`.

## TL;DR

- **Plexor** is a self-hosted cloud platform. v0.1 = in-memory node registry +
  PostgreSQL with one schema per module.
- Two parallel naming systems — see below. They look inconsistent at first;
  they aren't.
- The resource-scope hierarchy is **Organization → Team → Folder** (3 levels).
  Resources can live at any level (Org-wide for shared, Folder-narrow for
  private).

## Naming: architecture theme vs C# concept

Plexor uses **two naming systems in parallel** by design. Both are load-
bearing. Confusing them is the #1 source of agent mistakes in this repo.

| System | Where | Purpose | Examples |
|--------|-------|---------|----------|
| **Architecture theme** | PostgreSQL schema names, single-word one-token | Stable DB identifiers, no underscores, no spaces, hard to drift across migrations | `sigil`, `realm`, `atlas`, `ledger`, `forge`, `outpost`, `shard` |
| **C# concept** | Module names, entity names, type names, claim names | What developers + users actually see and call things; rich and self-describing | `Organization`, `Team`, `Folder`, `User`, `Role`, `AuditEntry` |

### Why two systems

- **Schema names** are short, no special characters, easy to type in raw
  SQL, easy to grep in migrations. They form a "theme" — every schema is
  one word, thematically related (medieval-fantasy: sigil, realm, forge,
  outpost, shard, ledger, atlas).
- **C# concept names** match the domain language (Plexor users see
  "Organization" and "Folder" in the UI, not "Realm" or "Atlas"). They
  use standard cloud-platform vocabulary (Organization/Team/Folder, with
  shared-resource visibility — see GCP / YC for the model).

### Mapping: schema ↔ module ↔ entities

| Schema | C# module project | Entities owned |
|--------|-------------------|-----------------|
| `sigil` | `Plexor.Modules.Identity` | `User`, `Role`, `RoleBinding`, `ApiKey`, `SshKey`, `RefreshToken`, `SigningKey` |
| `realm` | `Plexor.Modules.Organizations` | `Organization`, `Team`, `Folder` |
| `atlas` | `Plexor.Modules.Audit` | `AuditEntry` |
| `ledger` | `Plexor.Modules.Billing` | (planned) `Invoice`, `MeteringRecord` |
| `forge` | (planned) cluster fleet module | (planned) `Cluster` |
| `outpost` | (planned) node registry module | (planned) `NodeRecord` |
| `shard` | (planned) workloads module | (planned) `Workload` |

When you see `realm.x` in SQL or `Schemes.Realm` in C# — that's the
schema. When you see `Organization` or `Folder` in C# — that's the
concept. **Same thing, different name.** Read the code to find the
mapping; this table is the cheat sheet.

### Do NOT cross the streams

- ❌ Don't name a C# class `Realm` or `Ledger` or `Atlas` — those are
  schema names, not concept names. The class is `Organization`, `Invoice`,
  `AuditEntry`.
- ❌ Don't name a schema `users` or `organizations` — those aren't
  architecture theme. Use `sigil` / `realm`.
- ❌ Don't add the schema name to a class name (`RealmOrganization` or
  `OrganizationRealm`) — pick the concept name only.

## Resource-scope hierarchy

Every resource in Plexor (VMs, clusters, k8s instances, app providers)
has a **3-tier nullable scope** that places it in the org hierarchy.

| Scope | Visible to | Example |
|-------|-----------|---------|
| `OrgId` only (Team+Folder null) | Everyone in the org | `k8s-prod` cluster, shared `k8s-dev` cluster |
| `OrgId` + `TeamId` (Folder null) | Everyone in the team | team-shared DB, team-wide service account |
| `OrgId` + `TeamId` + `FolderId` | Everyone in the folder | per-folder VM, per-project app instance |

**Resources default to Folder-scoped.** Org-level and Team-level are
opt-in for shared resources (admin-only, audit-logged).

### Organization / Team / Folder

- **Organization** = top-level billing + auth boundary. Multi-org
  deploys are isolated; cross-org users are Phase 2+.
- **Team** = IAM aggregation. A team has 5-15 people. Role bindings
  can be scoped to a team (e.g. "team-zero admins"). Phase 1 ships
  the entity; team-scoped endpoints land in Phase 2.
- **Folder** = resource namespace. Where the actual resources
  (VMs, services, etc.) live by default. Multiple folders per team
  for separation of concerns (dev, staging, prod, project-alpha,
  project-beta).

### `ICurrentUser` shape (post-rename)

After the Org/Team/Folder rename is in (Phase A complete),
`ICurrentUser` exposes:

```csharp
Guid UserId       { get; }  // caller identity
Guid OrgId        { get; }  // organization
Guid? TeamId      { get; }  // team, if known/selected
Guid? FolderId    { get; }  // folder, if known/selected
IReadOnlyCollection<string> Roles        { get; }
IReadOnlyCollection<string> Permissions  { get; }
bool IsService              { get; }
```

JWT claims: `sub` (UserId), `org`, `team`, `folder`, `role`, `permission`,
`service`, `iss=plexor`.

## Migration order

Plexor uses EF Core migrations. The `Plexor.Migrator` CLI applies them in
FK-dependency order on startup:

1. `realm` (Organizations, Teams, Folders) — **always first**, every other
   table FKs into `realm.organizations.id`.
2. `sigil` (Identity) — FKs into `realm.organizations.id`.
3. `atlas` (Audit) — FKs into both `sigil.users.id` (actor) and
   `realm.organizations.id` (tenant scope).
4. (future) `ledger`, `forge`, `outpost`, `shard` — each depends on
   the modules above it.

When generating a new migration with `dotnet ef migrations add`, make
sure the target DbContext's dependencies (FKs) have already been
migrated to the target database.

## Build + verification

Single command gates every commit:

```bash
dotnet build plexor.slnx -c Debug
```

Build must be clean (0 warnings, 0 errors). What the build catches:
- `TreatWarningsAsErrors=true` in `Directory.Build.props` — every
  analyzer warning (CA-*, RCS-*, MA-*, IDE-*, VSTHRD-*) fails the build.
- Format drift — `VerifyFormatOnBuild` target.
- API style — `VerifyAntiPatternsOnBuild` target (no `this.x = x`,
  no `var x = ...; if (x is null)` patterns, etc.).

## When you get stuck

- Read `.agents/rules/` — every agent rule is there, with self-audit
  grep commands.
- Read `.agents/docs/architecture/` — design docs for each module /
  concern (identity.md, persistence.md, traffic.md, mcp.md, etc.).
- Read the existing code before writing new code. The conventions are
  in the code, not just the docs.
- If you find yourself about to write a C# class named after a
  schema (`RealmUser`, `LedgerOrder`), STOP. The mapping in this file
  is the convention.