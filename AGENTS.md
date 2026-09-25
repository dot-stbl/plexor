# AGENTS — Plexor project guide for AI agents and new contributors

> Read this first. It explains the two-name system (architecture theme vs
> C# concept), the resource-scope hierarchy, and the migration order.
> Skipping this is how you end up writing `realm.tenants` next to a class
> called `Organization`.

## Frontend work (web/)

Building or fixing a page/component in `web/apps/console`? Stop reading
here — go to `web/apps/console/AGENTS.md` instead. It's the entry point
for frontend work (routes, components, i18n, the `bun run shot`
visual-check loop). Everything below this section is C#/backend and
does not override it — the two guides cover disjoint parts of the repo.

## TL;DR

- **Plexor** is a self-hosted cloud platform. v0.1 = PostgreSQL with one
  schema per module. Nodes and clusters live in Postgres (`forge` /
  `Plexor.Modules.Clusters`). JWT signing keys live in the DB
  (`sigil.signing_keys`). CA key/cert and the Data Protection keyring
  live on disk under `/var/lib/plexor/` (see the backup admin page).
- Two naming systems in parallel (theme schema vs C# concept) — see
  below. They look inconsistent at first; they aren't.
- The resource-scope hierarchy is **Organization → Team → Folder** (3 levels).
  Resources can live at any level (Org-wide for shared, Folder-narrow for
  private).

## Naming: architecture theme vs C# concept

Plexor uses **two naming systems in parallel** by design. Both are load-
bearing. Confusing them is the #1 source of agent mistakes in this repo.

| System | Where | Purpose | Examples |
|--------|-------|---------|----------|
| **Architecture theme** | PostgreSQL schema names, single-word one-token | Stable DB identifiers, no underscores, no spaces, hard to drift across migrations | Theme-word: `sigil`, `realm`, `atlas`, `ledger`, `forge`, `outpost`, `shard`. Later modules used the concept noun as the schema (`branding`, `network`, `quotas`, `storage`) — same rule, no underscores. |
| **C# concept** | Entity / type / property / claim names — what devs + users call things | What developers + users actually see and call things; rich and self-describing | `Organization`, `Team`, `Folder`, `User`, `Role`, `AuditEntry` |

### Why two systems

- **Schema names** are short, no special characters, easy to type in raw
  SQL, easy to grep in migrations. Original modules used the architecture
  theme (sigil, realm, atlas, ledger, forge, outpost, shard). Later
  shipped modules used the concept noun (`quotas`, `branding`, `storage`,
  `network`). The C# project is the concept (`Plexor.Modules.Clusters`
  owns schema `forge`); do not assume schema == project name.
- **C# concept names** match the domain language (Plexor users see
  "Organization" and "Folder" in the UI, not "Realm" or "Atlas"). They
  use standard cloud-platform vocabulary (Organization/Team/Folder, with
  shared-resource visibility — see GCP / YC for the model).

### Mapping: schema ↔ module ↔ entities

| Schema | C# module project | Entities owned |
|--------|-------------------|-----------------|
| `sigil` | `Plexor.Modules.Sigil` | `User`, `Role`, `RoleBinding`, `ApiKey`, `SshKey`, `RefreshToken`, `SigningKey` |
| `realm` | `Plexor.Modules.Realm` | `Organization`, `Team`, `Folder`, `OrgAuthProviderConfig` |
| `atlas` | `Plexor.Modules.Audit` | `AuditEntry` |
| `forge` | `Plexor.Modules.Clusters` | `Cluster`, `Node`, `JoinToken`, `Workload`, `WorkloadLifecycleEvent`, `NodeCommand` |
| `forge` | `Plexor.Shared.Mtls` | `RevokedCert` (same schema as Clusters — revoke cascade shares the transaction) |
| `quotas` | `Plexor.Modules.Quotas` | `QuotaDefinition`, `QuotaAssignment`, `QuotaUsage`, `RateLimitEvent` |
| `branding` | `Plexor.Modules.Branding` | `GlobalThemeConfig`, `OrgThemeConfig`, `ThemeInstallation` |
| `storage` | `Plexor.Modules.Storage` | `Volume`, `Bucket` |
| `network` | `Plexor.Modules.Network` | `FloatingIp`, `LoadBalancer` |
| `outpost` | `Plexor.Providers.VSphere` | `VSphereInventorySnapshot`, `VSphereCluster`, `VSphereHost`, `VSphereVirtualMachine`, `VSphereProvisioningRun` |
| `ledger` | (planned) `Plexor.Modules.Billing` | (planned) `Invoice`, `MeteringRecord` |
| `shard` | (planned, unused) | workloads shipped in `forge` with Clusters; do not invent a second workloads schema |

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
the explicit `AddModuleDbContext` order in
`src/host/Plexor.Migrator/Program.cs` (FK-dependency order). That list
is the source of truth — not this table, if they ever diverge.

1. `realm` (`RealmDbContext`) — Organizations, Teams, Folders. **Always
   first**; every other table FKs into `realm.organizations.id`.
2. `sigil` (`IdentityDbContext`) — users, roles, keys. FKs into
   `realm.organizations.id`.
3. `forge` (`ClusterDbContext`) — clusters, nodes, join tokens,
   workloads. After Identity because nodes reference users; tenant
   rows carry `org_id` into `realm.organizations`.
4. `forge` (`RevokedCertsDbContext`, `Plexor.Shared.Mtls`) — mTLS
   revoke list. No extra FKs; shares the `forge` schema with Clusters.
5. `quotas` (`QuotasDbContext`) — isolated (polymorphic `ScopeId`, no
   FKs into Realm/Sigil). After them so a freshly-migrated schema can
   receive the catalog rows the seeder inserts.
6. `branding` (`BrandingDbContext`) — global + per-org theme.
7. `atlas` (`AuditDbContext`) — FKs into `sigil.users.id` (actor) and
   `realm.organizations.id` (tenant scope).
8. `storage` (`StorageDbContext`) — volumes, buckets (records only).
9. `network` (`NetworkDbContext`) — floating IPs, load balancers.
10. `outpost` (`VSphereDbContext`) — cached vSphere inventory.

When generating a new migration with `dotnet ef migrations add`, make
sure the target DbContext's dependencies (FKs) have already been
migrated to the target database. Adding a DbContext means adding a
call in `Program.cs` — the compiler will not silently miss it.

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
