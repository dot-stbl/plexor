# Change: phase-5-audit

## Why

v0.1 ships a structured-logging audit emitter
(`LoggingQuotaAuditEmitter`) so quota state changes leave a trace,
but the trace lives in stdout — there is no place to query
"What did user X do last Tuesday?" and no durable storage
beyond the host's log rotation. For a self-hosted single-tenant
deploy this is acceptable (the operator has the logs); for
multi-tenant SaaS the absence of a queryable, retention-bound
audit log becomes a hard gap the moment compliance shows up.

Phase 5 ships the durable audit trail: a new
`Plexor.Modules.Audit` module owns the `atlas.audit_entries`
table; existing audit emitters swap from `ILogger` to
`IDbAuditEmitter` behind the same `IAuditEmitter` interface;
operators get a paginated REST query endpoint behind a new
permission string; a daily retention `BackgroundService` prunes
old rows.

## What

### Schema

New table `atlas.audit_entries` (schema `atlas`, owned by
`Plexor.Modules.Audit`):

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | UUID v7 PK |
| `org_id` | `uuid` (NULL) | tenant scope when applicable |
| `actor_user_id` | `uuid` (NULL) | caller — null for system events |
| `action` | `varchar(256)` | stable dot.case wire name (`quotas.assignment.changed`, `theme.installed.activated`) |
| `target_type` | `varchar(64)` (NULL) | `QuotaAssignment`, `Workload`, `Org`, … |
| `target_id` | `uuid` (NULL) | id of the target entity |
| `payload_json` | `jsonb` (NULL) | event-specific structured data |
| `occurred_at` | `timestamptz` | UTC, index column |

Indexes: `INDEX (org_id, occurred_at DESC)`,
`INDEX (actor_user_id, occurred_at DESC)`,
`INDEX (action, occurred_at DESC)`.

### Module

`Plexor.Modules.Audit` (Domain / Application / Infrastructure /
Api). Domain entity: `AuditEntry` (the only entity in v0.1).
Application interfaces:

- `IAuditEmitter.EmitAsync(AuditEvent, ct)` — single emit
  contract; no-throw (`Task` returns; failures are logged
  at `Critical`, never propagated).
- `IUnitOfWorkAuditTransactionScope` — optional bridge to the
  resource-create transaction when the caller wants the
  audit row to commit atomically with the resource write
  (e.g. quota assignment changes inside the quotas PUT).
  Used by `EfQuotaEnforcer` today.

### Implementations

v0.1 ships two implementations:

- `LoggingAuditEmitter` — keeps writing structured log lines
  (the v0.0 path). Marked `[Obsolete]` for callers; retained
  for tests + the migration window.
- `DbAuditEmitter` — INSERTs into `atlas.audit_entries`.
  Default registration in v0.1.

The existing `IQuotaAuditEmitter` becomes a thin wrapper that
delegates to `IAuditEmitter` and re-applies the `quotas.*`
namespace on `Action`.

### REST endpoint

- `GET /api/v1/audit` (`audit.read`) — paginated query of
  `atlas.audit_entries` filtered by:
  - `org_id` (forced to `current.OrgId` — tenant-scoped)
  - `actor_user_id` (optional)
  - `action` prefix (optional, e.g. `quotas.*`)
  - `occurred_at` range (`from` / `to`, unix ms per
    Plexor's wire format)

Response: page envelope `{ items, total, next_cursor }`.

### Permission

New string `audit.read` (mirrors `quotas.read`). Granted to
the built-in `admin` role by default; the `viewer` role
also gets it (auditors are read-only operators).

### Retention

`AuditRetentionBackgroundService` (`IHostedService`) — daily
at 03:00 UTC, runs:

```sql
DELETE FROM atlas.audit_entries
  WHERE occurred_at < now() - interval '90 days';
```

The 90-day window is configurable via
`[Audit] RetentionDays` (default 90; min 30). Retention MUST
NOT delete rows that are referenced by an unresolved open
investigation (Phase 5+ adds the case-management table — not
in v0.1).

## Impact

- **`quotas` capability** — `IQuotaAuditEmitter` becomes a
  thin wrapper around `IAuditEmitter`. The four event wire
  names (`quotas.assignment.changed`,
  `quotas.assignment.removed`, `quotas.usage.exceeded`,
  `quotas.limit.approaching`) remain unchanged.
- **`identity` capability** — one new permission string
  (`audit.read`). The additive delta is in
  `openspec/changes/phase-5-audit/specs/identity/spec.md`.
- **`Plexor.Host/Program.cs`** — registers `AddAuditModule()`
  (the new installer), swaps the audit emitter DI binding
  from `LoggingAuditEmitter` to `DbAuditEmitter`, wires the
  retention hosted service.
- **`Plexor.Migrator`** — adds `InitAudit` migration; runs
  after `atlas` schema is created.

## Out of scope (later phases)

- **Case management** — Phase 5+ adds an
  `atlas.investigation_cases` table so retention can pause
  for open cases. v0.1 retention is unconditional.
- **Audit export (CSV / S3)** — operators query the API; no
  bulk export. Phase 5+.
- **Audit UI in the console** — the admin "Audit" page ships
  separately; this change ships only the REST endpoint.
- **Immutable / WORM storage** — v0.1 trusts the database.
  Phase 5+ considers append-only S3 buckets for
  regulatory-driven deploys.
- **Per-actor token issuance trail** — the `auth-providers`
  capability already emits `OrgAuthProviderChanged` events;
  per-token sign/verify is not logged in v0.1.
