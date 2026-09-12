# Change: phase-4-5-quotas

## Why

Today nothing in Plexor stops a single self-hosted user from
filling every VM slot, every gigabyte of Ceph RBD, every floating
IP, and every load balancer the host can offer. There is no
upper bound between "what the operator asked for" and "what the
host can deliver"; the first user to scale up saturates the
host. For a single-tenant self-host this is acceptable — the
admin is the only consumer — but the same code will be the
control plane of a future multi-tenant SaaS deploy. Without
quotas a noisy neighbor can degrade the platform for everyone
else.

Phase 4.5 ships capacity control: per-organization (and
per-team, per-folder) limits on resource counts and cumulative
capacity, plus sliding-window API rate limits so a runaway
caller can't drown the control plane. Quotas are the capacity-
side counterpart to billing (Phase 2+) — quotas prevent
over-consumption, billing accounts for what was consumed.

## What

A new module `Plexor.Modules.Quotas` owns the capability. Schema
name in SQL: `quotas`. Four entities:

- `QuotaDefinition` — the catalog of limit keys (e.g.
  `compute.vms.count`, `compute.vms.vcpu`,
  `compute.vms.ram_gb`, `storage.volumes.count`,
  `storage.volumes.gb`, `network.floating_ips.count`,
  `network.load_balancers.count`,
  `api.requests.per_hour.user`,
  `api.requests.per_hour.org`). Stable keys, versioned via
  `EffectiveFrom` / `EffectiveUntil`.
- `QuotaAssignment` — the value bound to a scope. Polymorphic
  scope: `(scope_type, scope_id)` where `scope_type` is
  `Org` / `Team` / `Folder` and `scope_id` is the matching id.
  Effective value = minimum across folder → team → org →
  default.
- `QuotaUsage` — current consumption snapshot, one row per
  `(scope_type, scope_id, definition_key)`. Updated inside
  the resource-create transaction.
- `RateLimitEvent` — append-only log for sliding-window API
  rate limiting. Indexed on `(principal_id, occurred_at)`.

Plus:

- `IQuotaEnforcer` in `Plexor.Shared.Kernel` with
  `CheckAndReserveAsync(scope, definitionKey, amount, ct)`.
  Implementation uses Postgres `pg_advisory_xact_lock` at scope
  granularity, taken inside the resource-create transaction
  (atomic with the INSERT). No async reconcile.
- `IRateLimiter` in `Plexor.Shared.Kernel` with sliding-window
  count via `SELECT COUNT(*) FROM rate_limit_events WHERE
  principal_id = $1 AND occurred_at > now() - interval '1 hour'`.
  Per call: one SELECT + one INSERT.
- Tiered enforcement: 80% → `AllowedWithWarning(thresholdPct=80)`,
  UI shows a badge + response carries `X-Quota-Warning: true`
  header. 100% → `Denied` → `QuotaExceededException` → 429
  ProblemDetails with `code = "quotas.exceeded"`. No admin
  override flow in v1 — admin raises the limit.
- Default assignments seeded by the `Plexor.Migrator` on org
  creation (compute.vms.count=100, compute.vms.vcpu=256,
  compute.vms.ram_gb=512, storage.volumes.count=200,
  storage.volumes.gb=4096, network.floating_ips.count=20,
  network.load_balancers.count=20,
  api.requests.per_hour.user=10000,
  api.requests.per_hour.org=100000). Idempotent — skip on
  re-run.
- REST endpoints: `GET /api/v1/quotas/definitions`,
  `GET /api/v1/quotas/assignments?scope=org|team|folder&id=X`,
  `PUT /api/v1/quotas/assignments`,
  `DELETE /api/v1/quotas/assignments/{id}`,
  `GET /api/v1/quotas/usage?scope=...&id=...`,
  `GET /api/v1/quotas/effective?scope=...&id=...`. FluentValidation
  on PUT (value > 0, scope exists, definition exists).
  `[RequirePermission]` on each endpoint.
- New identity permissions: `quotas.read` (granted to all
  authenticated users), `quotas.assign.org` (granted to the
  built-in `admin` role by default).
- Wire the enforcer into Compute (`CreateVm`,
  `CreateWorkload`), Storage (`CreateVolume`), and Network
  (`CreateFloatingIp`, `CreateLoadBalancer`) create paths.
  Pre-check + reserve call inside the resource-create
  transaction. Failed resource creation rolls back the
  reservation.

## Impact

- **`identity` capability** — two new permission strings
  (`quotas.read`, `quotas.assign.org`) added to the role
  permission catalog. Built-in `admin` role gains
  `quotas.assign.org` by default. See
  `openspec/changes/phase-4-5-quotas/specs/identity/spec.md`
  for the additive delta.
- **`realm` capability** — folder / team scoping already
  supports scope targets via the 3-tier hierarchy. No changes
  required to `Organization`, `Team`, or `Folder`.
- **`compute`, `storage`, `network` modules** — each
  resource-create path adds a pre-check + reserve call inside
  its existing DB transaction. Failed resource creation rolls
  back the reservation automatically.
- **`Plexor.Shared.Kernel`** — adds `IQuotaEnforcer` and
  `IRateLimiter` interfaces, `QuotaScope` record, and
  `QuotaCheckResult` discriminated union.
- **`Plexor.Migrator`** — adds `OrgSeeder` (default
  assignments) registered alongside the existing
  `IdentityAdminSeeder`.
- **`Plexor.Host/Program.cs`** — registers `IQuotaEnforcer`,
  `IRateLimiter`, `RateLimitFilter` (IAsyncActionFilter), and
  the `QuotasModule` endpoints.

## Out of scope (later phases)

- **Team-scoped admin permissions** — `quotas.assign.team`,
  `quotas.assign.folder`. Defer to Phase 2 when the Team /
  Folder admin roles land. v1 ships `quotas.assign.org` only.
- **Self-service quota-raise UI** — operator flow for an
  org admin to request a raise; requires admin approval
  workflow. Phase 5+.
- **Per-region quotas** — one cluster per host today;
  per-region capacity tracking lands with multi-cluster in
  Phase 7+.
- **OAuth2 third-party clients** — quotas apply to first-
  party callers only in v1. External OAuth2 client scopes
  ship with the OAuth module in v0.3+.
- **Token-bucket rate limiting** — sliding window is the v1
  algorithm. Reconsider if bursty traffic becomes a concern
  (likely Phase 5+ with the OAuth work).
- **Quotas for app-provider workloads** — quotas cover Plexor
  resources (VMs, volumes, IPs, LBs). App-provider resources
  (WordPress sites, Postgres instances) inherit the host VM /
  volume quotas; per-app-provider quotas are a future
  extension.
- **Cross-org quota sharing** — quotas are per-org by default.
  Shared pools (e.g. a parent org granting capacity to child
  orgs) are not modeled.