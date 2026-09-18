# Spec delta: audit (phase-5-audit)

This file is the proposed state of
`openspec/specs/audit/spec.md` after the change
`openspec/changes/phase-5-audit/` is merged. It uses the
`## ADDED Requirements` convention from OpenSpec — every
requirement here is a new requirement being introduced by the
change.

When the change lands, this delta is promoted into
`openspec/specs/audit/spec.md` under `## Requirements`, and
the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: AuditEntry table

The system SHALL persist audit events in the
`AuditEntry` entity
(`Plexor.Modules.Audit.Domain.Entities.AuditEntry`,
schema `atlas.audit_entries`).

`AuditEntry` SHALL carry:

- `Id : Guid` — UUID v7 PK.
- `OrgId : Guid?` — tenant scope when applicable; null for
  system events (e.g. retention sweep).
- `ActorUserId : Guid?` — caller identity; null for system
  events.
- `Action : string` — stable dot.case wire name
  (`quotas.assignment.changed`,
  `theme.installed.activated`).
- `TargetType : string?` — short noun for the affected
  entity (`QuotaAssignment`, `Workload`, `Org`, …).
- `TargetId : Guid?` — id of the affected entity.
- `PayloadJson : string?` — event-specific JSON
  (`jsonb` column; structured data the auditor queries).
- `OccurredAt : DateTimeOffset` — UTC, index column.

Indexes: `INDEX (org_id, occurred_at DESC)`,
`INDEX (actor_user_id, occurred_at DESC)`,
`INDEX (action, occurred_at DESC)`.

### Requirement: IAuditEmitter abstraction

The system SHALL expose `IAuditEmitter`
(`Plexor.Modules.Audit.Application.IAuditEmitter`) with
`EmitAsync(AuditEvent, ct)`. `AuditEvent` is the call-site
shape: `(OrgId?, ActorUserId?, Action, TargetType?, TargetId?,
PayloadJson?)`.

`IAuditEmitter.EmitAsync` MUST NOT throw. A failed emit is
logged at `LogLevel.Critical` with the full event as
structured properties; the caller's request succeeds.

Two implementations ship in v0.1:

- `DbAuditEmitter` (default) — INSERTs into
  `atlas.audit_entries`.
- `LoggingAuditEmitter` (`[Obsolete]`) — retained for the
  migration window + tests; emits a structured log line.

The composition root (`Plexor.Host/Program.cs`) wires
`DbAuditEmitter` as the default.

### Requirement: IQuotaAuditEmitter becomes a wrapper

The `IQuotaAuditEmitter` interface
(`Plexor.Shared.Kernel.Quotas.IQuotaAuditEmitter`) SHALL
be re-implemented as a thin wrapper around `IAuditEmitter`:

- The wrapper's `EmitAsync` constructs an `AuditEvent` with
  `Action = <quotaEvent.WireName>` (the stable dot.case name
  on `QuotaAuditEventExtensions`) and forwards to
  `IAuditEmitter.EmitAsync`.
- The four wire names are unchanged:
  `quotas.assignment.changed`,
  `quotas.assignment.removed`,
  `quotas.usage.exceeded`,
  `quotas.limit.approaching`.

Call sites (`EfQuotaEnforcer`, `QuotasController`) are
unchanged. `LoggingQuotaAuditEmitter` is deleted.

### Requirement: GET /api/v1/audit query endpoint

The system SHALL expose
`GET /api/v1/audit` (`audit.read`) with the following query
parameters:

- `actorUserId : Guid?` — filter to a specific caller.
- `actionPrefix : string?` — dot.case prefix match
  (`quotas.*`).
- `fromUnixMs : long?` — occurred_at lower bound (inclusive).
- `toUnixMs : long?` — occurred_at upper bound (exclusive).
- `cursor : string?` — opaque pagination cursor.
- `limit : int = 50` — page size, capped at 500.

Response: `PageResponse<AuditEntryResponse>` — the standard
Plexor page envelope
(`{ items, total, next_cursor }`).

Tenant-scoped: the `orgId` filter is forced to
`currentUser.OrgId`. A user in Org X SHALL NOT see entries
from Org Y.

Validation: `fromUnixMs <= toUnixMs`, `toUnixMs` not in the
future (rejects `toUnixMs > clock.GetUtcNow().ToUnixTimeMilliseconds()`
+ 60s clock skew). All enforced by FluentValidation.

### Requirement: audit.read permission

The system SHALL expose a new permission string `audit.read`
in the role permission catalog
(`Plexor.Modules.Sigil.Domain.Entities.Role.Permissions`).
The permission SHALL be granted to both the built-in `admin`
role and the built-in `viewer` role by default (the Migrator
SHALL include `audit.read` in both roles' permissions
during seed).

`audit.read` SHALL gate `GET /api/v1/audit`.

Missing permission SHALL return HTTP 403 with
`code = "identity.permission.denied"`.

### Requirement: Audit retention background service

The system SHALL expose
`AuditRetentionBackgroundService`
(`Plexor.Modules.Audit.Infrastructure`) as an
`IHostedService` that runs daily at 03:00 UTC and executes:

```sql
DELETE FROM atlas.audit_entries
  WHERE occurred_at < now() - interval '<RetentionDays> days';
```

`RetentionDays` SHALL be configurable via the
`[Audit] RetentionDays` option
(`Plexor.Modules.Audit.Application.AuditOptions`).
Default: 90. Range: 30–3650. The `AddAuditModule()` installer
SHALL bind + validate the option with
`ValidateDataAnnotations().ValidateOnStart()`.

Retention is unconditional in v0.1 — no per-row hold. A
future case-management table (Phase 5+) extends the DELETE
to skip rows in open investigations.

### Requirement: Audit events from the org-auth-provider controller

`OrgAuthProvidersController` SHALL emit the following audit
events on every state-changing call, behind `IAuditEmitter`:

- `PUT /api/v1/iam/orgs/{orgId}/auth-provider` succeeds →
  `action = "org.auth_provider.changed"`,
  `target_type = "OrgAuthProviderConfig"`,
  `payload_json = { "provider": "sigil" | "oidc", "rotation": true|false }`.
- `PUT` switches `Sigil` → `Oidc` →
  `action = "org.auth_provider.changed"` with
  `payload_json = { "provider": "oidc" }`.
- `POST /api/v1/iam/orgs/{orgId}/auth-provider/test` succeeds →
  `action = "org.auth_provider.test_connected"` with
  `payload_json = { "discoveryDocumentUrl": "<url>",
  "availableScopes": [...] }`.
- `POST .../test` fails (timeout, HTTP error, malformed
  discovery) →
  `action = "org.auth_provider.test_failed"` with
  `payload_json = { "error": "<reason>" }`.

The four wire names map directly to `atlas.audit_entries.action`.
The plaintext OIDC client secret is NEVER included in
`payload_json`.

### Requirement: Migration order

`atlas.audit_entries` migrations SHALL be applied after the
`sigil` schema (FK in spirit — the seeder enumerates sigil
rows when enriching with actor info) and before or alongside
the `quotas` schema (quota assignments audit rows). The
`Plexor.Migrator` orders schemas by FK dependency:
`realm` → `sigil` → `atlas` → `quotas`. The
`InitAudit` migration is the first `atlas` migration.

### Requirement: Audit module installer

The system SHALL expose `AddAuditModule()` as the single
composition-root extension
(`Plexor.Modules.Audit.Application.AuditModuleExtensions.AddAuditModule`)
that:

1. Registers `AuditDbContext` (Scoped, via the shared
   `NpgsqlDataSource`).
2. Registers `DbAuditEmitter` as the default `IAuditEmitter`
   (Scoped; matches the DbContext lifetime).
3. Registers `AuditRetentionBackgroundService` as a hosted
   service.
4. Binds + validates `AuditOptions` with
   `ValidateDataAnnotations().ValidateOnStart()`.
5. Wires the `audit.read` permission string into the
   Migrator's `IdentityBootstrapper` seed.

`AddAuditModule()` SHALL be called once in
`Plexor.Host/Program.cs` and once in
`Plexor.Migrator/Program.cs` (the Migrator needs the schema
present for the seed to run).

## ADDED Key Entities

### `AuditEntry`

`Plexor.Modules.Audit.Domain.Entities.AuditEntry`
(schema `atlas.audit_entries`). Fields as listed above.

### `AuditEvent`

`Plexor.Modules.Audit.Application.AuditEvent` — the
call-site shape: `(OrgId?, ActorUserId?, Action, TargetType?,
TargetId?, PayloadJson?)`. Sealed record, init-only
properties.

### `AuditOptions`

`Plexor.Modules.Audit.Application.AuditOptions` — the
options record for the retention window:

- `RetentionDays : int` (range 30–3650, default 90).

### `AuditPermissions`

`Plexor.Shared.Kernel.Audit.AuditPermissions` —
permission-string constants: `Read = "audit.read"`,
`AdminWildcard = "*"`.
