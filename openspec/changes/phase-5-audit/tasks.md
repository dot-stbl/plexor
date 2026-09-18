# Tasks: phase-5-audit

Numbered checklist. Each sub-section is one or more commits,
each independently buildable against the current state of
`plexor.slnx`.

## 5.1 — Audit module + DbAuditEmitter + quota delegation

- [x] Add `Plexor.Modules.Audit` project structure
      (`Domain/Application/Infrastructure/Api`).
- [x] Domain entity: `AuditEntry` (init-only properties;
      `OccurredAt` indexed).
- [x] Application interface: `IAuditEmitter` with
      `EmitAsync(AuditEvent, ct)`. `AuditEvent` carries
      `OrgId?`, `ActorUserId?`, `Action`, `TargetType?`,
      `TargetId?`, `PayloadJson?`.
- [x] EF DbContext (`AuditDbContext`) +
      `IEntityTypeConfiguration<AuditEntry>` (snake_case,
      schema `atlas`, jsonb for `payload_json`).
- [x] Migration `InitAudit` via
      `dotnet ef migrations add InitAudit --context AuditDbContext`.
- [x] `LoggingAuditEmitter` (kept for the migration window,
      `[Obsolete]`).
- [x] `DbAuditEmitter` (Infrastructure, internal) — INSERTs
      into `atlas.audit_entries`, no-throw contract.
- [x] `IQuotaAuditEmitter` rewritten as a thin wrapper around
      `IAuditEmitter` with the `quotas.*` namespace; the
      EfQuotaEnforcer and QuotasController call sites stay
      unchanged.
- [x] Unit tests: `DbAuditEmitterShould` (5 cases) +
      `QuotaAuditEmitterShould` (3 cases).

## 5.2 — Audit query endpoint

- [x] `AuditController` with `GET /api/v1/audit`
      (`audit.read`).
- [x] Query filters: `orgId` (forced to currentUser.OrgId),
      `actorUserId?`, `actionPrefix?`, `fromUnixMs?`,
      `toUnixMs?`.
- [x] Response: page envelope
      (`{ items, total, next_cursor }`).
- [x] FluentValidation on the query: `from < to`, `to` not in
      the future.
- [x] Permission string `audit.read` added to
      `Plexor.Shared.Kernel.Audit.AuditPermissions`.
- [x] Unit tests: `AuditControllerShould` (4 cases).

## 5.3 — Org auth provider change event

- [x] `OrgAuthProvidersController.Upsert` emits
      `org.auth_provider.changed` (`action = "oidc.switched"`
      or `"sigil.reset"`).
- [x] `OrgAuthProvidersController.Test` emits
      `org.auth_provider.test_connected` /
      `org.auth_provider.test_failed`.
- [x] Wire `IAuditEmitter` into the controller via constructor
      injection (already there for the quota delegator).

## 5.4 — Retention service + admin UI

- [x] `AuditRetentionBackgroundService` (`IHostedService`)
      runs daily at 03:00 UTC.
- [x] Configurable window via `[Audit] RetentionDays` (default
      90, min 30) — `AddAuditModule()` registers the options
      with `ValidateDataAnnotations().ValidateOnStart()`.
- [x] Admin UI page: `AdminAuditPage` (FE) + `AuditListPage`
      (FE) under `/admin/audit`.
- [x] FE component tests: `AdminAuditPage.test.tsx` (3 cases)
      + `AuditListPage.test.tsx` (4 cases).

## 5.5 — Cleanup (sealed AuditDbContext, invariant fix)

- [x] Seal `AuditDbContext` (project convention; concrete
      DbContext classes are `sealed`).
- [x] `BrandingGlobalSeederHostedService` invariant — the
      seeded row is upserted (idempotent), not asserted as
      present. Pre-existing bug.
- [x] `Api/Models/` split into `Requests/` + `Responses/` in
      the branding module (file-organization cleanup).
