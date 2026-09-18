# Design: phase-5-audit

Technical decisions for Phase 5 audit. Each section explains
why a particular shape was chosen over the alternatives
considered.

## Module placement

New module `Plexor.Modules.Audit`. Schema name `atlas`.

**Rationale.** Audit is a cross-cutting concern that every
module writes to. Putting it in a shared kernel project would
couple the kernel to EF Core; putting it in any single feature
module would make the other modules depend on that module's
internals. A standalone `Plexor.Modules.Audit` with the
`atlas` schema is the same modular-monolith pattern Plexor
already uses for quotas / branding — bounded context, single
owner, one migration.

The architecture-theme name `atlas` carries the meaning of
"load-bearing map of what happened" — a deliberate nod to the
Greek titan condemned to hold up the sky.

## AuditEvent shape vs AuditEntry row

`IAuditEmitter.EmitAsync` takes a lightweight `AuditEvent`
record (the call-site surface):

```csharp
public sealed record AuditEvent(
    Guid? OrgId,
    Guid? ActorUserId,
    string Action,             // dot.case wire name
    string? TargetType,        // short noun ("QuotaAssignment")
    Guid? TargetId,
    string? PayloadJson);      // pre-serialized JSON string
```

The DB row (`AuditEntry`) is the persisted projection:
`AuditEvent` → `DbAuditEmitter` does the field-by-field
INSERT. The two shapes are intentionally close so the mapping
is trivial; the record lives in `Plexor.Modules.Audit.Application`,
the entity in `Plexor.Modules.Audit.Domain`.

## No-throw contract

`IAuditEmitter.EmitAsync` MUST NOT throw. Audit emission
failure must never break a user request — losing an audit row
is a problem for the auditor, not the user. The
`DbAuditEmitter` wraps the INSERT in try/catch and records
failures at `LogLevel.Critical` with the full event context
as structured properties.

Why not a fire-and-forget `Channel<AuditEvent>` +
`BackgroundService` consumer? Two reasons:

1. **Caller-side coupling to transaction.** The quota PUT
   wants the audit row to commit atomically with the
   assignment write — if the assignment succeeds but the
   audit row rolls back, the audit log diverges from reality.
   The inline await + same-transaction INSERT is the only
   way to guarantee atomicity.
2. **Audit log divergence.** A background consumer that
   crashes mid-batch loses rows silently. The inline path
   surfaces every failure as a `Critical` log line.

If write latency becomes a concern (it hasn't in v0.1 — a
single INSERT per request is sub-millisecond on the warm
pool), Phase 5+ can introduce an outbox-pattern consumer.

## Retention: unconditional daily DELETE

`AuditRetentionBackgroundService` runs daily at 03:00 UTC and
executes:

```sql
DELETE FROM atlas.audit_entries
  WHERE occurred_at < now() - interval '<RetentionDays> days';
```

The DELETE is unconditional in v0.1. The case-management
table that would let retention pause for open investigations
ships later. The 90-day default is long enough for typical
audit windows (PCI-DSS wants 12 months online + archival;
SOC 2 wants 90 days; HIPAA wants 6 years). 90 is the SOC 2
minimum.

The retention interval is configurable via
`[Audit] RetentionDays` (range 30–3650) — operators with
regulatory requirements can extend the window. Min 30 days
prevents accidental "delete everything" foot-guns.

## Query endpoint shape

`GET /api/v1/audit` follows the Plexor page-envelope pattern
(`{ items, total, next_cursor }`):

- `from` / `to` — unix ms timestamps (per
  `openspec/specs/identity/spec.md` §"Wire format").
- `actionPrefix` — optional dot.case prefix filter
  (`quotas.*` matches every quota event).
- `actorUserId` — optional UUID filter.
- `cursor` — opaque pagination cursor (UUID v7 of the last
  seen row + a tiebreaker on `occurred_at`).

Page size is fixed at 50 in v0.1 (matches the audit-page FE
default). Larger pages require `?limit=100` — server caps at
500 to bound memory.

## Permission model

| Permission | Granted to | Purpose |
|---|---|---|
| `audit.read` | `admin` (built-in), `viewer` (built-in) | Query the audit log |

The `viewer` role already grants a read-only footprint — the
auditor persona is a viewer with `audit.read` plus the
existing `quotas.read`. No new role is minted in v0.1; the
Migrator's `IdentityBootstrapper` seeds the existing roles
with the new permission strings.

## Why `IAuditEmitter` not `IQuotaAuditEmitter`-style per-module

The quota module shipped `IQuotaAuditEmitter` first because
v0.0 didn't have a generic audit module — quotas needed
audit and there was nowhere else to put it. With Phase 5 the
generic interface exists; `IQuotaAuditEmitter` is the
backward-compat wrapper that re-applies the `quotas.*`
namespace prefix and forwards to `IAuditEmitter`. The
`LoggingQuotaAuditEmitter` is deleted; the existing call
sites (`EfQuotaEnforcer`, `QuotasController`) stay
unchanged because the wrapper has the same shape.

Future modules (storage, network, clusters) wire
`IAuditEmitter` directly — no per-module wrapper.

## Tenant isolation

Every audit query is forced to `currentUser.OrgId`. A user
authenticated in Org X SHALL NOT see audit entries from Org
Y. The EF Core global query filter on `AuditEntry.OrgId`
provides defense-in-depth at the database layer; the
controller adds an explicit `WHERE org_id = current.OrgId`
filter to make the intent unambiguous to readers.

`audit.read` is granted to admins by default; admin
impersonation across tenants is Phase 5+ and lands behind a
separate `audit.admin` permission string.

## Open questions deferred

- **Append-only / WORM storage.** v0.1 trusts the database;
  a malicious operator with SQL access can delete rows.
  Phase 5+ considers immutable S3 buckets + cryptographic
  chain hashing for regulatory-driven deploys.
- **Audit forwarding (SIEM).** No outbox to Splunk / Elastic
  in v0.1. The structured log path is the only forward.
- **Per-actor rate / anomaly detection.** Phase 5+.
- **Audit of admin impersonation.** Phase 5+ when the
  multi-tenant deploy lands.
