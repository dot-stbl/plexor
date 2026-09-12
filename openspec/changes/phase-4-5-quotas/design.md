# Design: phase-4-5-quotas

Technical decisions for Phase 4.5 quotas. Each section explains
why a particular shape was chosen over the alternatives
considered.

## Module placement

New module `Plexor.Modules.Quotas` (standalone). Schema name
`quotas`.

**Rationale.** Quotas and billing are separate concerns:
quotas prevent over-consumption (capacity control); billing
accounts for what was consumed (cost recovery). The two
systems read different data — quotas track live counter
deltas inside resource-create transactions; billing reads
post-commit metering rows and produces invoices on a schedule.
Putting quotas inside `Plexor.Modules.Billing` would couple
two lifecycles that need to ship independently (quotas must
land before the first multi-tenant deploy; billing is Phase
2+).

Naming `Quotas` (rather than `Limits` or `Capacity`) matches
the operator-facing vocabulary — the dashboard will read
"quotas", not "limits". The schema name `quotas` follows the
Plexor naming theme (architecture theme = one-word one-token).

## Schema split

| Schema | Tables | Owner |
|--------|--------|-------|
| `quotas` | `quota_definitions`, `quota_assignments`, `quota_usage`, `rate_limit_events` | `Plexor.Modules.Quotas` |
| `ledger` (future) | `invoices`, `metering_records` | `Plexor.Modules.Billing` |

The `quotas` schema is a clean ownership boundary. The
`ledger` schema ships later (Phase 2+) for billing; the two
modules MUST NOT share tables. If a future requirement needs
to join quota usage to billing, the join happens in the
read-side controller (not in the schema).

## Enforcement: pure-sync with `pg_advisory_xact_lock`

The `IQuotaEnforcer.CheckAndReserveAsync` flow:

```sql
BEGIN;
  SELECT pg_advisory_xact_lock(<scope-hash>);
  -- resolve effective assignment via scope walker
  SELECT value FROM quota_assignments
    WHERE scope_type = $1 AND scope_id = $2
      AND definition_key = $3 FOR UPDATE;
  SELECT current_value FROM quota_usage
    WHERE scope_type = $1 AND scope_id = $2
      AND definition_key = $3 FOR UPDATE;
  -- compute delta; if current + amount > limit → ROLLBACK + 429
  UPDATE quota_usage SET current_value = current_value + $4 ...;
  -- caller INSERTs the resource row here, in the same TX
COMMIT;
```

Properties:

- **Atomic.** The lock + reservation + resource INSERT all
  commit (or rollback) together. No async reconcile job. No
  "we reserved it but the resource creation failed" gap.
- **No over-spend.** Two concurrent `Compute.CreateVm`
  calls for the same `(scope, compute.vms.count)` serialise
  on the advisory lock; the second call observes the
  post-reserve value of the first.
- **Cost.** Concurrent creates for the same scope are
  serialised. Acceptable for self-host scale (≤ 1 host) and
  for the early multi-tenant deploys where one org is the
  primary tenant. Multi-host / multi-cluster would revisit
  this with partition-keyed locks (Phase 7+).

**Alternative considered — row-level lock on `quota_usage`
row.** Rejected: the row may not exist yet (first INSERT for
that scope), and the advisory lock is lighter than taking a
row lock on a not-yet-existing tuple. The advisory lock is
also released automatically by Postgres at COMMIT/ROLLBACK
(transaction-scoped), so there's no cleanup path.

## Tiered enforcement (80% warn, 100% block)

- 80% → `AllowedWithWarning(thresholdPct = 80)`. The
  endpoint succeeds; the UI shows a warning badge; the
  response carries `X-Quota-Warning: true`.
- 100% → `Denied`. `QuotaExceededException` → 429
  ProblemDetails with `code = "quotas.exceeded"`. Body
  shape: standard RFC 9457 with `extensions.code`,
  `extensions.limit`, `extensions.used`, `extensions.requested`.

**No admin override flow in v1.** The reasoning: an "allow
this one over the limit" path opens the system to pressure
from operators to bump users past their cap. The correct
response to over-limit is "raise the limit" (admin edits the
assignment) — not "skip the limit". The admin path is the
official path; the bypass path is not modeled.

## Rate-limit algorithm: sliding window via event log

The `RateLimitEvent` table is append-only, indexed on
`(principal_id, occurred_at)`. Per-request:

```sql
SELECT COUNT(*) FROM rate_limit_events
  WHERE principal_id = $1
    AND occurred_at > now() - interval '1 hour';

INSERT INTO rate_limit_events (id, principal_id, occurred_at)
  VALUES ($1, $2, now());
```

Properties:

- **Accurate.** Sliding window is a true rate computation,
  not a coarse bucket.
- **Cheap.** One SELECT + one INSERT per request, both
  index-hit operations. A daily cleanup BackgroundService
  deletes events older than the max period (1h for v1).
- **Inspectable.** The table is queryable for analytics
  (which callers are getting close to their limit) and for
  forensics (a runaway caller's history).

**Alternative considered — token bucket.** Rejected for v1
because sliding window is easier to reason about and the
operator-facing vocabulary ("1000 requests per hour") maps
1:1 to the storage shape. Token bucket adds tunable
parameters (refill rate, burst capacity) without a v1 use
case. Reconsider if bursts become a concern (Phase 5+).

## Period semantics

`QuotaDefinition.Period` is one of `none`, `hour`, `day`,
`month`. v1 uses only `none` and `hour`:

- `period = none` — value is an absolute limit (resource
  counts, GB, vCPU). The `quota_usage.current_value` counter
  is the lifetime count.
- `period = hour` — value is per-hour (rate-limit per hour).
  The `quota_usage.current_value` counter is reset at the
  start of each hour bucket.

`day` and `month` are reserved for future billing-style
windows (e.g. "10 TB of egress per calendar month"). v1 does
not emit them; the catalog seed is `none` and `hour` only.

## Permission model

| Permission | Granted by default | Purpose |
|-----------|---------------------|---------|
| `quotas.read` | All authenticated users | View definitions, assignments, usage, effective values |
| `quotas.assign.org` | Built-in `admin` role | Create / update / delete assignments at the org scope |

**Team- and folder-scoped assign permissions are deferred
to Phase 2**, when the Team / Folder admin roles land. v1
ships org-scoped assignment only — the dashboard can still
target a team or folder in the URL, but only an admin with
`quotas.assign.org` can edit.

The two permission strings are added to the role permission
catalog in this change; the additive delta to the identity
capability is in
`openspec/changes/phase-4-5-quotas/specs/identity/spec.md`.

## Inline enforcement vs. event-driven consumption update

Two ways to keep `QuotaUsage.current_value` in sync:

1. **Inline (chosen).** `Compute.CreateVm` does
   `CheckAndReserveAsync` inside its own DB transaction.
   `QuotaUsage.current_value` is updated atomically with the
   resource INSERT. No consumer / event handler.
2. **Event-driven.** Compute publishes `ResourceCreated`,
   the Quotas module consumes and updates `QuotaUsage` on
   the event.

**Why inline.** Eventual consistency is not safe here: a
quota-enforcer call that observes stale `current_value`
will over-spend. The inline approach is atomic and simple
to reason about; the event-driven approach would require
idempotency keys, outbox pattern, and reconciliation jobs
to be safe. v1 ships inline; event-driven is reserved for
the case where the resource-create path can no longer
afford the cost of an inline lock (very high write rate).

The domain event `ResourceCreated` / `ResourceDeleted` is
still emitted — but for audit consumption
(`QuotaAssignedChanged`, `QuotaUsageExceeded`,
`QuotaLimitApproaching` events), not for quota-state
synchronization.

## Open questions deferred

- **Token bucket vs sliding window.** Sliding window is
  chosen; revisit if bursty traffic becomes a concern
  (Phase 5+).
- **Per-region quotas.** Phase 7+ with multi-cluster
  deploys. Today's data model carries `OrgId` + `TeamId` +
  `FolderId` but not region; adding region to the scope
  walker is a forward-compatible change.
- **Burst allowance for power users.** No v1.
- **Per-app-provider quotas.** App-provider resources
  (WordPress sites, Postgres instances) inherit the host VM
  / volume quotas. Per-app-provider quotas are a future
  extension; the catalog seed in 4.5.a does not include
  them.
- **Quota-raise approval workflow.** v1 has only the admin
  direct-edit path. A self-service raise request workflow
  is Phase 5+.