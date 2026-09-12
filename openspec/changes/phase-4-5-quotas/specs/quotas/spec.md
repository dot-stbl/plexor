# Spec delta: quotas (phase-4-5-quotas)

This file is the proposed state of
`openspec/specs/quotas/spec.md` after the change
`openspec/changes/phase-4-5-quotas/` is merged. It uses the
`## ADDED Requirements` convention from OpenSpec — every
requirement here is a new requirement being introduced by the
change.

When the change lands, this delta is promoted into
`openspec/specs/quotas/spec.md` under `## Requirements`, and
the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: QuotaDefinition catalog

The system SHALL expose a catalog of limit keys via
`QuotaDefinition`
(`Plexor.Modules.Quotas.Domain.Entities.QuotaDefinition`)
rows in `quotas.quota_definitions`. v1 ships the following
keys:

- `compute.vms.count` — number of VMs.
- `compute.vms.vcpu` — cumulative vCPU across VMs.
- `compute.vms.ram_gb` — cumulative RAM GiB across VMs.
- `storage.volumes.count` — number of volumes.
- `storage.volumes.gb` — cumulative volume GiB.
- `network.floating_ips.count` — number of floating IPs.
- `network.load_balancers.count` — number of load
  balancers.
- `api.requests.per_hour.user` — sliding-window request
  count per user.
- `api.requests.per_hour.org` — sliding-window request
  count per organization.

Every `QuotaDefinition` SHALL expose a stable `Key` (the
catalog identifier), a human-readable `Description`, a
`Period` (`none` | `hour` | `day` | `month`; v1 uses
`none` and `hour`), and an `EffectiveFrom` /
`EffectiveUntil` pair (null = currently effective).

Catalog keys SHALL be stable across versions — adding a new
key is a non-breaking change; renaming or removing a key is
a breaking change requiring a migration step.

### Requirement: QuotaAssignment polymorphic scope

The system SHALL expose per-scope limit values via
`QuotaAssignment`
(`Plexor.Modules.Quotas.Domain.Entities.QuotaAssignment`)
rows in `quotas.quota_assignments`. A row binds a
`DefinitionKey` to a scope via `(ScopeType, ScopeId)` where
`ScopeType` is `Org` | `Team` | `Folder` and `ScopeId` is
the matching Realm entity id.

`QuotaAssignment` SHALL have the following invariants:

- Exactly one row per `(ScopeType, ScopeId, DefinitionKey,
  Period)` (enforced by DB UNIQUE constraint).
- The same `DefinitionKey` MAY appear at multiple scopes for
  the same Realm tree (e.g. org-wide 100 VMs + folder-specific
  10 VMs).
- Removing a `QuotaAssignment` SHALL NOT remove the
  corresponding `QuotaUsage` row — the usage counter persists
  so a future re-assignment resumes from the same value.

### Requirement: Effective value resolution

The system SHALL resolve the effective value for a
`(scope, definition_key)` request by walking the Realm
hierarchy: folder → team → org → default, and returning the
**minimum** of the values found.

- If a folder-scoped assignment exists for `DefinitionKey`,
  the folder value wins (regardless of whether a team or
  org-scoped assignment also exists).
- Else, if a team-scoped assignment exists for
  `DefinitionKey`, the team value wins.
- Else, if an org-scoped assignment exists for
  `DefinitionKey`, the org value wins.
- Else, the catalog's built-in default value wins.

The walker MUST be deterministic and MUST return a
`QuotaCheckResult` that records which scope's value was
selected (so audit entries can cite the source).

### Requirement: IQuotaEnforcer with atomic check-and-reserve

The system SHALL expose `IQuotaEnforcer`
(`Plexor.Shared.Kernel.Abstractions.IQuotaEnforcer`) with
`CheckAndReserveAsync(scope, definitionKey, amount, ct)` that:

1. Acquires a Postgres advisory transaction-scoped lock at
   scope granularity
   (`pg_advisory_xact_lock(<scope-hash>)`).
2. Resolves the effective assignment value via the scope
   walker.
3. Reads + locks the corresponding `QuotaUsage` row
   (`SELECT ... FOR UPDATE`).
4. Computes `current + amount` vs `effective_limit`.
5. Returns `Allowed` if `current + amount ≤ effective_limit`.
6. Returns `AllowedWithWarning(thresholdPct = 80)` if
   `current + amount > 0.8 * effective_limit` and the call
   would still succeed.
7. Returns `Denied` (with current + effective limit +
   requested amount in the result) if the call would exceed
   the limit.

The lock + the `QuotaUsage` UPDATE + the caller's resource
INSERT SHALL all run in the **same** DB transaction. A
failed resource creation SHALL roll back the reserved quota.

### Requirement: Tiered enforcement at 80% / 100%

The system SHALL emit a tiered signal from the enforcer:

- 80% threshold reached (caller's request succeeds but
  `current + amount > 0.8 * effective_limit`) →
  `AllowedWithWarning(thresholdPct = 80)`. The HTTP response
  carries `X-Quota-Warning: true`. The UI shows a warning
  badge next to the resource.
- 100% reached (caller's request would exceed
  `effective_limit`) → `Denied`. The handler throws
  `QuotaExceededException`, mapped to HTTP 429 ProblemDetails
  with `code = "quotas.exceeded"`, `extensions.limit`,
  `extensions.used`, `extensions.requested`.

No admin override / bypass flow SHALL exist in v1. The
correct path past the limit is to raise the assignment
(admin edits `QuotaAssignment.Value`).

### Requirement: IRateLimiter with sliding window

The system SHALL expose `IRateLimiter`
(`Plexor.Shared.Kernel.Abstractions.IRateLimiter`) with
sliding-window count via the `rate_limit_events` table.
Per-call, the limiter SHALL:

1. `SELECT COUNT(*) FROM rate_limit_events WHERE
   principal_id = $1 AND occurred_at > now() - interval '1
   hour'`.
2. INSERT one event row (cheap, single PK).
3. Return `Allowed` if `count + 1 ≤ limit`, else `Denied`
   with `RetryAfter` (seconds until the oldest in-window
   event expires).
4. Return `AllowedWithWarning` at the 80% threshold (same
   signal shape as the quota enforcer).

A daily `BackgroundService` SHALL delete events older than
the max period (1h) to keep the table bounded.

### Requirement: Rate-limit applies to both principal and org

The system SHALL check rate limits against two principals:

- `principal_id` — the user id (JWT auth) OR the API key id
  (service auth).
- `org_id` — the organization id (aggregate).

`IRateLimiter` SHALL check both, returning the more
restrictive result (the lower of the two limits wins). A
runaway service account SHALL be blocked by its org's
limit even if its own limit is high.

### Requirement: Default assignments seeded per org

The system SHALL seed a `QuotaAssignment` row for every
`QuotaDefinition` in the catalog, scoped to the
organization, on org creation. Defaults:

| Key | Default value |
|-----|---------------|
| `compute.vms.count` | 100 |
| `compute.vms.vcpu` | 256 |
| `compute.vms.ram_gb` | 1024 |
| `storage.volumes.count` | 200 |
| `storage.volumes.gb` | 4096 |
| `network.floating_ips.count` | 10 |
| `network.load_balancers.count` | 20 |
| `api.requests.per_hour.user` | 1000 |
| `api.requests.per_hour.org` | 10000 |

Seeding is idempotent: re-running the seeder on an
existing org SHALL NOT overwrite existing assignments.

The seeder runs in `Plexor.Migrator` on first deploy and on
new org creation. The same `OrgSeeder` MUST also be
registered as a domain event consumer for the org-created
event so SaaS-deploy-created orgs get seeded without a
re-migration.

### Requirement: REST endpoints for quota management

The system SHALL expose the following endpoints, all behind
`Plexor.Shared.Authorization.RequirePermissionAttribute`:

- `GET /api/v1/quotas/definitions` (`quotas.read`) — return
  the catalog.
- `GET /api/v1/quotas/assignments?scope=org|team|folder&id=X`
  (`quotas.read`) — list assignments at the given scope.
- `PUT /api/v1/quotas/assignments` (`quotas.assign.org`)
  — create or update an assignment.
- `DELETE /api/v1/quotas/assignments/{id}`
  (`quotas.assign.org`) — remove an assignment.
- `GET /api/v1/quotas/usage?scope=...&id=...`
  (`quotas.read`) — return current usage for the scope.
- `GET /api/v1/quotas/effective?scope=...&id=...`
  (`quotas.read`) — return the resolved effective value
  per definition key for the scope.

`PUT` SHALL be validated by FluentValidation: `value > 0`,
scope exists, definition exists in the catalog. Endpoints
are tenant-scoped — a user in Org X SHALL NOT view or
assign quotas for Org Y (enforced via the
`current.OrgId` filter in the controllers).

### Requirement: Audit integration

The system SHALL emit the following audit events to
`atlas.audit_entries`:

- `QuotaAssignedChanged` — emitted on every successful
  PUT or DELETE of `QuotaAssignment`. Includes old value,
  new value, scope, definition key, actor id.
- `QuotaUsageExceeded` — emitted when the enforcer returns
  `Denied`. Includes the requested amount, the limit, the
  current usage, the principal, and the scope.
- `QuotaLimitApproaching` — emitted when the enforcer first
  crosses the 80% threshold for a `(scope, definition_key)`
  pair. Suppressed if already emitted within the last 24
  hours to avoid log spam.

The `atlas` schema is owned by `Plexor.Modules.Audit`; the
quotas module writes audit entries through the standard
`IAuditWriter` abstraction.

### Requirement: Migration order

The `quotas` schema migrations SHALL run after `realm` and
`sigil` migrations and before (or alongside) `atlas`. The
`Plexor.Migrator` orders schemas by FK dependency: `realm` →
`sigil` → `atlas` → `quotas`. `QuotaAssignment.ScopeId` does
not require a DB-level FK to `realm.organizations.id` (it is
polymorphic across `Org` / `Team` / `Folder`), but the
runtime MUST validate the scope id exists in the appropriate
Realm table before persisting an assignment.

## ADDED Key Entities

### `QuotaDefinition`

`Plexor.Modules.Quotas.Domain.Entities.QuotaDefinition`
(schema `quotas.quota_definitions`). Fields:

- `Id : Guid` — UUID v7, PK.
- `Key : string` — stable catalog identifier
  (`compute.vms.count`).
- `Description : string` — human-readable label.
- `Period : QuotaPeriod` — `none` | `hour` | `day` | `month`.
- `DefaultValue : long` — catalog default if no assignment
  exists.
- `Unit : string` — human-readable unit (`vms`, `vcpu`, `gb`,
  `requests`).
- `EffectiveFrom : DateTimeOffset?` — null = currently
  effective.
- `EffectiveUntil : DateTimeOffset?` — null = no scheduled
  retirement.

### `QuotaAssignment`

`Plexor.Modules.Quotas.Domain.Entities.QuotaAssignment`
(schema `quotas.quota_assignments`). Fields:

- `Id : Guid` — UUID v7, PK.
- `ScopeType : QuotaScopeKind` — `Org` | `Team` | `Folder`.
- `ScopeId : Guid` — id of the matching Realm entity.
- `OrgId : Guid` — denormalized for tenant-scoped queries.
- `DefinitionKey : string` — references
  `QuotaDefinition.Key`.
- `Value : long` — the limit value for this scope.
- `CreatedAt : DateTimeOffset`, `UpdatedAt : DateTimeOffset`.

`UNIQUE (ScopeType, ScopeId, DefinitionKey, Period)`.

### `QuotaUsage`

`Plexor.Modules.Quotas.Domain.Entities.QuotaUsage`
(schema `quotas.quota_usage`). Fields:

- `Id : Guid` — UUID v7, PK.
- `ScopeType : QuotaScopeKind`.
- `ScopeId : Guid`.
- `OrgId : Guid` — denormalized.
- `DefinitionKey : string`.
- `CurrentValue : long` — current consumption. Reset at
  period boundary for `hour` / `day` / `month`.
- `PeriodStart : DateTimeOffset` — current period start.
- `UpdatedAt : DateTimeOffset`.

`UNIQUE (ScopeType, ScopeId, DefinitionKey, PeriodStart)`.

### `RateLimitEvent`

`Plexor.Modules.Quotas.Domain.Entities.RateLimitEvent`
(schema `quotas.rate_limit_events`). Fields:

- `Id : Guid` — UUID v7, PK.
- `PrincipalId : Guid` — user id or API key id.
- `OrgId : Guid` — denormalized for org-aggregate queries.
- `Kind : string` — `user` | `api_key` | `org`.
- `Endpoint : string` — request path (bounded cardinality).
- `OccurredAt : DateTimeOffset` — UTC timestamp.

`INDEX (PrincipalId, OccurredAt)`,
`INDEX (OrgId, OccurredAt)`.

### Value Objects and Exceptions

- `QuotaScopeKind` (enum) — `Org` | `Team` | `Folder`.
- `QuotaScope` (record) — `(ScopeType, ScopeId, OrgId)`.
- `QuotaDefinitionKey` (record) — typed wrapper for
  `string`.
- `QuotaCheckResult` (discriminated record) — `Allowed` |
  `AllowedWithWarning(thresholdPct, limit, used, requested)`
  | `Denied(limit, used, requested)`.
- `RateLimitResult` (discriminated record) — `Allowed` |
  `AllowedWithWarning(thresholdPct)` | `Denied(retryAfter)`.
- `QuotaException` — base, stable code
  `quotas.exceeded`.
- `QuotaExceededException` — thrown by handlers when
  `Denied` is returned. Mapped to 429 ProblemDetails.