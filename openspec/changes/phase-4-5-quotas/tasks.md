# Tasks: phase-4-5-quotas

Numbered checklist. Each sub-section (`4.5.a` through `4.5.h`)
is one or more commits, each independently buildable against the
current state of `plexor.slnx`.

## 4.5.a — QuotaDefinition seed + Quota schema

- [ ] Add `Plexor.Modules.Quotas` project structure
  (`Domain/Application/Infrastructure/Api`).
- [ ] Add `Plexor.Modules.Quotas` project to `plexor.slnx`.
- [ ] Domain entities: `QuotaDefinition`, `QuotaAssignment`,
  `QuotaUsage`, `RateLimitEvent`.
- [ ] Value objects: `QuotaScopeKind` enum, `QuotaCheckResult`
  discriminated record, `QuotaScope` record.
- [ ] `QuotaException` + `QuotaExceededException` with stable
  error code (`quotas.exceeded`).
- [ ] EF DbContext + `IEntityTypeConfiguration<T>` per entity
  (snake_case naming, schema `quotas`).
- [ ] Migration `InitQuotas` via
  `dotnet ef migrations add InitQuotas --context QuotasDbContext`.
- [ ] Migrator seed: `QuotaDefinition` catalog (built-in
  defaults from proposal — `compute.vms.count`,
  `compute.vms.vcpu`, `compute.vms.ram_gb`,
  `storage.volumes.count`, `storage.volumes.gb`,
  `network.floating_ips.count`,
  `network.load_balancers.count`,
  `api.requests.per_hour.user`,
  `api.requests.per_hour.org`).

## 4.5.b — IQuotaEnforcer + scope walker

- [ ] `IQuotaEnforcer` interface in `Plexor.Shared.Kernel`
  (with `QuotaScope`, `QuotaCheckResult`,
  `QuotaDefinitionKey`).
- [ ] `IQuotaScopeResolver` — walks folder → team → org →
  default, returns the effective value.
- [ ] `EfQuotaEnforcer` implementation: `pg_advisory_xact_lock`
  + `SELECT FOR UPDATE` + `UPDATE` in the same DB transaction.
- [ ] Period-aware value resolution for hour / day / month
  scopes.
- [ ] Warning threshold (80%) → `AllowedWithWarning` result;
  100% → `Denied`.
- [ ] Unit tests: enforcer resolution, lock contention,
  threshold boundaries.

## 4.5.c — Wire into Compute module

- [ ] `Compute.CreateVm` path: pre-check + reserve before
  INSERT (transaction scope).
- [ ] Verify: creating a VM under the limit succeeds, at the
  limit returns 429 with `code = "quotas.exceeded"`.
- [ ] Verify: rollback works (failed VM create releases the
  reserved quota).
- [ ] `Compute.CreateWorkload` (or similar) — second resource
  type for proof-of-pattern.

## 4.5.d — Wire into Storage + Network modules

- [ ] `Storage.CreateVolume` path: pre-check + reserve for
  `storage.volumes.count` and `storage.volumes.gb`.
- [ ] `Network.CreateFloatingIp` path: pre-check + reserve for
  `network.floating_ips.count`.
- [ ] `Network.CreateLoadBalancer` path: pre-check + reserve
  for `network.load_balancers.count`.

## 4.5.e — IRateLimiter + RateLimitFilter

- [x] `IRateLimiter` interface in `Plexor.Shared.Kernel`.
- [x] `EfRateLimiter`: sliding-window via
  `SELECT COUNT(*)` + INSERT into `rate_limit_events` per call.
- [x] `RateLimitResult`: `Allowed` / `AllowedWithWarning` /
  `Denied` (with `RetryAfter`).
- [x] `RateLimitFilter : IAsyncActionFilter` — runs after auth,
  before controller.
- [x] Principal resolution: user from JWT, api_key from claims.
- [x] Two principals checked: `principal_id` (user or key),
  `org_id` (aggregate).
- [x] Cleanup BackgroundService — daily delete events older
  than the max period (1h for v1).

## 4.5.f — OrgSeeder (default assignments)

- [ ] `OrgSeeder` in `Plexor.Migrator` — runs on first deploy
  AND on new org creation.
- [ ] Idempotent: skip if the assignment already exists for
  `(definition, scope)`.
- [ ] Default values from the proposal table.
- [ ] Unit tests: idempotency, default value correctness.

## 4.5.g — REST endpoints

- [ ] `GET /api/v1/quotas/definitions` (catalog).
- [ ] `GET /api/v1/quotas/assignments?scope=org|team|folder&id=X`.
- [ ] `PUT /api/v1/quotas/assignments`.
- [ ] `DELETE /api/v1/quotas/assignments/{id}`.
- [ ] `GET /api/v1/quotas/usage?scope=...&id=...`.
- [ ] `GET /api/v1/quotas/effective?scope=...&id=...`.
- [ ] FluentValidation on PUT (value > 0, scope exists,
  definition exists).
- [ ] `[RequirePermission]` on each endpoint.
- [ ] OpenAPI / Scalar metadata.

## 4.5.h — Audit integration

- [ ] Domain event `ResourceCreated` / `ResourceDeleted`
  published by Compute / Storage / Network modules.
- [ ] `QuotasModule` consumer updates `QuotaUsage` on event
  (alternative to inline; pick one — inline chosen for now,
  document why in `design.md`).
- [ ] `AuditEntry` entries for: `QuotaAssignedChanged`,
  `QuotaUsageExceeded`, `QuotaLimitApproaching` (80%
  threshold crossing).