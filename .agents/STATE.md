# STATE — canonical current position of Plexor

> **Read this FIRST.** Single source of truth: что построено, на каком этапе,
> какие решения зафиксированы, какой drift уже разрешён. Заменяет собой
> «текущее состояние» из `HANDOFF.md` (тот теперь — только про стиль общения
> и build-конвенции; состояние живёт здесь).
>
> Версия: 0.3.1 · обновлено: 2026-07-19

---

## Что строим

**Plexor** — self-hosted cloud platform (YC / Hetzner-класса) для команды `.stbl`.
Один control-plane (`Plexor.Host`) + N compute-нод (`Plexor.NodeAgent`), которые
присоединяются zero-touch через join-токен + mTLS. Расширяемость — provider-plugin
(install providers built-in, app providers через marketplace-шаблоны).

Stack: **.NET 10** modular monolith + Vite/React портал. Подробнее — `docs/architecture.md`.

## Топология — self-hosted (КАНОН, решено 2026-07-08)

Plexor — **single-cluster self-hosted fleet**, НЕ Yandex-Cloud multi-tenant.

- Роуты портала **top-level**: `/vms`, `/clusters`, `/networks`, `/billing`, `/audit`.
  НЕ `/projects/{pid}/...`. VM и Cluster — параллельные first-class ресурсы.
- **Cluster** = один `Plexor.Host` + ноды, вступившие по join-токену + mTLS-сертификат.
  Юзер сам ставит control-plane (ISO / `plx init`), UI отдаёт join-токен + CA-bundle,
  node сохраняет client cert и join'ится. Networking — WireGuard mesh + VXLAN overlay
  + internal DNS (см. `docs/architecture/networking.md`).
- **Tenant / Project** остаются в домене/scope как уровни изоляции, но в MVP UI
  работает как **single-tenant**. Multi-tenant слой — **Phase 2+**, не сейчас.

## Текущая позиция (честно)

| Слой | Состояние | Где |
|---|---|---|
| Фундамент (репо, build-гейты, .NET-скелет, правила) | ✅ готово | `plexor.slnx`, `.agents/rules/` |
| Дизайн-прототип (6 экранов HTML + styleguide + launcher) | ✅ готово | `.agents/docs/design/` |
| Портал (React, на MSW-моках) | 🟡 в работе, на паузе | `web/apps/console/` |
| **Backend domain (.NET)** | **✅ MVP-готов + Tier 3-5 закрыты (3.5 тоже)** | `src/modules/`, `src/shared/` |
| mTLS (host↔node) | ✅ end-to-end | `src/shared/security/Plexor.Shared.Mtls/` |
| Capabilities auto-detect | ✅ архитектура + baseline probe | `src/shared/infra/Plexor.Shared.Capabilities/` |
| Installer (`plx init`) + node join | 🟡 Phase B done (mTLS end-to-end), Phase C/D остались | `plan-mvp-secure-deploy` |

**Ключевое изменение с 2026-07-08:** backend **.NET существует и работает end-to-end**
— Identity, Clusters, Workloads (control-plane side + data plane), mTLS, Capabilities,
Configuration, Identifiers, Compute abstractions. **Phase D Tier 1-5 + Tier 3.5 закрыты
(2026-07-19):** Workloads schema + REST (Tier 1+2) → WorkloadKind sealed + Cluster.RuntimeId
(Tier 1+2 финализация) → Docker Compose / Podman Quadlet / k3s runtime providers (Tier 3/4/5
runtime-provider axis) → LibvirtKvm/Lxc/Qemu reference impls (Tier 3.5) → drift detection
BackgroundService + per-node command queue (Tier 4-5 control-plane axis) → action
endpoints start/stop/restart через mTLS (Tier 5). Workload loop реальный: control-plane
state → mTLS → runtime provider → обратный state.

**Что осталось закрыть в backend:**
- Tier 3.5 reference impls не все подключены в installer (Docker Compose — primary для
  v0.1, k3s/Podman — за ним)
- Phase D installer (`plx init` end-to-end) — Phase C/D в `.agents/docs/plans/plan-mvp-secure-deploy.md`

## Глобальные этапы (продукт)

| # | Этап | Статус |
|---|---|---|
| Э0 | Фундамент: репо, build-гейты, .NET-структура, правила, дизайн-система | ✅ |
| Э1 | UI-портал на моках (экраны + shadcn DS + kubb/MSW) | 🟡 (на паузе пока backend не закрыт) |
| Э2 | OpenAPI-контракты → kubb-codegen (реальные контроллеры) | ⏳ |
| Э3 | Domain model + первый модуль **Compute/Identity/Clusters** (.NET) | ✅ |
| Э4 | Installer (`plx init`) + install providers + node join | 🟡 (Phase B mTLS done) |
| Э5 | Остальные модули: Network, Storage, Billing, Marketplace | ⏳ |
| **Э6** | **`Plexor.NodeAgent` / data plane (реальное исполнение workloads)** | **✅ Tier 1-5 + 3.5 done** — runtime providers (Docker Compose / Podman Quadlet / k3s) + LibvirtKvm/Lxc/Qemu reference impls + drift detection + action endpoints |
| Э7 | Workloads runtime loop (Docker provider, drift detection, action endpoints) | **✅ закрыт в рамках Э6** |

## Backend — что реально построено (по модулям)

### Plexor.Shared — 15 проектов, 4 группы (kernel / persistence / security / infra)

| Проект | Что внутри | Тесты |
|---|---|---|
| `Plexor.Shared.Kernel` | CQRS / ICommandHandler / outbox / background-services base | — |
| `Plexor.Shared.Contracts` | Pagination, Filters, Routes | — |
| `Plexor.Shared.NodeApi` | NodeAgent ↔ Control-plane контракты | — |
| `Plexor.Shared.Workloads` | IWorkloadProvider, LocalWorkload, WorkloadState enum | — |
| `Plexor.Shared.Persistence` | EF Core base, Repository<T>, Specification<T, TResult>, PageAsync | — |
| `Plexor.Shared.Filtering` | DSL парсер (filter/sort/paging) + FilterableFieldSet | 115 |
| `Plexor.Shared.Authorization` | PermissionPolicyProvider, RequirePermissionAttribute | 17 |
| **`Plexor.Shared.Mtls`** | **X509Authority, PlexorCaFileStore, RevokedCertCache, CertAuthorityOptions** | **4** |
| **`Plexor.Shared.Identifiers`** | **ClusterId/NodeId/TokenId/WorkloadId (Prefixed UUIDv7 + IParsable<T>)** | **14** |
| `Plexor.Shared.Http` | HTTP клиенты (Refit) | — |
| `Plexor.Shared.Telemetry` | OpenTelemetry wiring + Console formatter | — |
| `Plexor.Shared.Composition` | Composition helpers | — |
| `Plexor.Shared.Console` | CLI-стилизация | — |
| **`Plexor.Shared.Configuration`** | **PLX_* env + TOML provider + `~/.plexor/` dot-dir convention** | **21** |
| **`Plexor.Shared.Capabilities`** | **ICapabilityProbe + NodeCapabilityAggregator + HostBridgeCapabilityProbe** | **7** |

### Plexor.Modules.* — 3 модуля (Identity / Realm / Clusters)

| Модуль | Schema | Тесты | Что готово |
|---|---|---|---|
| `Plexor.Modules.Sigil` (Identity) | `sigil` | 8 | JWT auth, ApiKey, RefreshToken, RoleBinding, PermissionPolicyProvider, RequirePermission attribute — **полный модуль** |
| `Plexor.Modules.Realm` (Tenants/Teams/Folders) | `realm` | 0 | Базовые entities + DbContext, без handler-ов (Phase 2+) |
| **`Plexor.Modules.Clusters`** | `forge` | **40** | **Cluster + Node + JoinToken + Workload** — Create/Update/Delete/Join/Heartbeat/List, REST `/api/v1/compute/clusters/*` + `/workloads/*` + action endpoints start/stop/restart, forge schema со всеми таблицами, типизированный heartbeat drift detection |

### Plexor.NodeAgent — control-plane клиент (Tier 1-5 + 3.5)

- Join flow: получает join-токен + CA-bundle, выпускает client cert, persist в `~/.plexor/`
- mTLS HTTP client (SocketsHttpHandler + cert pinning)
- Heartbeat loop с hardware snapshot
- In-memory workload registry
- **✅ Compute abstractions** (IImageRegistry / IVolumeBackend / INetworkBackend) +
  **3 reference impls**: `LibvirtKvmProvider`, `LibvirtLxcProvider`, `QemuProvider`
- **✅ Runtime providers** (через `IWorkloadProvider` + `WorkloadKind` discriminated):
  Docker Compose (`d377762`), Podman Quadlet (`321231e`), k3s + Kustomize renderer (`40c0bfa`)
- **✅ HttpImageRegistry** (`6db90b3`) — pull base images on demand
- **✅ NodeAgent Dockerfile** (`a8d1a13`) — production image
- **✅ Tier 4 control-plane loop**: heartbeat-driven drift detection (`277ac41`)
- **✅ Tier 5**: per-node command queue + action endpoints start/stop/restart через mTLS (`cddbfc9`)
- **✅ Tier 3.5**: LibvirtLxc + Qemu adopt compute abstractions (`dcaa736`)

## Identity & Auth — закрытые вопросы

- ✅ JWT bearer (Plexor.Modules.Sigil.Application.Auth)
- ✅ API key + refresh token (rotating, hashed storage)
- ✅ Permission policy provider (per-resource, per-action scopes)
- ✅ mTLS для Host↔NodeAgent (CN=`node_<NodeId>`, cert = credential, no JWT)
- ⏳ Dual auth-provider layer (Plexor Sigil + external OIDC per tenant) — отдельный plan

## Cluster & Compute — закрытые вопросы

- ✅ Cluster CRUD + REST endpoints
- ✅ Node join с cert issuance (CN pattern, 7-day TTL token)
- ✅ Heartbeat с hardware snapshot
- ✅ forge schema (clusters, nodes, join_tokens, workloads)
- ✅ Prefixed UUIDv7 ID scheme (cluster_, node_, tok_, wl_)
- ✅ Workloads Tier 1+2 (schema + entity + REST API)
- ✅ Workloads Tier 3 (Docker Compose runtime provider в Plexor.NodeAgent)
- ✅ Workloads Tier 4 (Podman Quadlet provider + drift detection BackgroundService)
- ✅ Workloads Tier 5 (k3s provider + action endpoints start/stop/restart через mTLS)
- ✅ Tier 3.5 (LibvirtKvm / LibvirtLxc / Qemu reference impls под compute abstractions)

## Architecture — зафиксированные решения (НЕ пересматривать без запроса)

Продуктовые / стек:
- Продукт **Plexor**, CLI-префикс **`plx`** (NativeAOT), **.NET 10**.
- **Modular monolith + node agents** (НЕ микросервисы).
- Provider pattern: `IProvider` + `I<Resource>Provider`; app providers = YAML-шаблоны (НЕ NuGet/plugin).
- DB **PostgreSQL** + EF Core 10 + Dapper; bus **NATS** (отложен, v0.x без него); mapper **Riok.Mapperly**; validation **FluentValidation**; API-docs **Scalar** / Microsoft.AspNetCore.OpenApi source-gen.
- UI: **Vite + React + TS + shadcn/ui (на Base UI) + Tailwind v4 + TanStack Router/Query + Zustand**; codegen **kubb**.

Архитектурные темы:
- **Schema-per-module в одной PostgreSQL базе (Architecture theme)** — `sigil`, `realm`, `forge`, `revoked_certs`. Один connection string, schemas дают изоляцию таблиц per bounded context (OpenStack nova/neutron/cinder pattern).
- **Prefixed UUIDv7 ID scheme** — `cluster_`, `node_`, `tok_`, `wl_` (через UUIDNext library). `IParsable<T>` на каждом ID типе для ASP.NET Core model binding.
- **mTLS, не JWT, для Host↔NodeAgent** — Cert IS the credential. CN = `node_<NodeId>`. PEM storage (PFX deprecated ctor replaced с `X509CertificateLoader.LoadCertificate` + `RSA.ImportFromPem`).
- **ICapabilityProbe pattern** — каждый runtime provider реализует свой probe. Shared проект знает только контракт + aggregator + HostBridgeCapabilityProbe (baseline). НЕ монолитный NodeProbe с 8 private методами.
- **Cross-platform paths через `~/.plexor/` dot-dir** — как `~/.aws/`, `~/.kube/`, `~/.docker/`. Не XDG. Production overrides через env var.
- **Plexor config stack** — PLX_* env (flat single-underscore: `PLX_DATABASE_HOST` → `Database:Host`) + TOML at `<UserProfile>/.plexor/config.toml` + appsettings.json dev default. `AddPlexorConfiguration()` wires TOML + PLX_ env vars last.
- **EF migrations tool-only** — `dotnet ef migrations add/remove` единственный sanctioned путь. Hand-edit = silent invalidation of snapshot.
- **Class decomposition rule** — файл > 300 строк ИЛИ > 2 private методов = signal to refactor (inject collaborator → extract service → file static class → extension method). Без `#region` директив.
- **30000-38999** зарезервировано под HTTP API control plane, **48000-48999** под node agent / runtime сервисы.
- **Docker runtime** для workloads (Phase D), KVM/LXC — провайдер-плагины Phase 2+.

## Phase D Workloads — Tier tracker

| Tier | Описание | Статус |
|---|---|---|
| **1** | Schema + entity + EF config + tool-generated migration | ✅ `dcb3f30` |
| **2** | Application layer (CQRS handlers) + REST controller | ✅ `49fba54`, `3708d22` |
| **2.1** | WorkloadKind sealed records + mapper; Cluster.RuntimeId field | ✅ `5f97c7e`, `403e376` |
| **3** | Docker Compose runtime provider (control-plane ↔ Plexor.NodeAgent) | ✅ `d377762` |
| **3.5** | LibvirtKvm / LibvirtLxc / Qemu reference impls под compute abstractions | ✅ `81afdf6`, `2a2b5d8`, `6fa073c`, `dcaa736` |
| **4** | Podman Quadlet provider + drift detection BackgroundService (heartbeat-driven) | ✅ `321231e`, `277ac41` |
| **5** | k3s provider + Kustomize renderer + action endpoints start/stop/restart через mTLS + per-node command queue | ✅ `40c0bfa`, `cddbfc9` |

**Итого Phase D (2026-07-19):** все Tier закрыты. Следующий инкремент —
Phase D installer (`plx init` end-to-end) и Tier 3.5 reference impls подключение
к installer.

## UI — экраны (порядок и статус)

Приоритет по персонам — см. `docs/ui/personas.md`. Брифы — `docs/ui/screens/`.

| Экран | Бриф | Код | Статус |
|---|---|---|---|
| VM list | `screens/01-vm-list.md` | `/vms` | ✅ построен, итерируется |
| Clusters (list + detail) | `screens/00-clusters.md` | `/clusters`, `/clusters/$id` | ✅ построен (self-hosted) |
| Create VM | `screens/03-create-vm-wizard.md` | `/vms/new` | ✅ построен |
| VM detail | `screens/02-vm-detail.md` | — | ⏭ следующий (backend готов, можно привязывать к реальному API) |
| Networks / VPC | `screens/04-network-vpc.md` | `/networks` (заглушка) | ⏳ |
| Billing / Usage | `screens/05-billing-usage.md` | `/billing` (заглушка) | ⏳ |
| Audit log | `screens/06-audit-log.md` | `/audit` (заглушка) | ⏳ |
| Storage (Volumes/Buckets) | — | — | ⏳ |
| IAM (Users/SSH/API keys) | `screens/99-future-screens.md` | — | ⏳ |
| Marketplace | `docs/ui/ui-inventory.md §7` | — | ⏳ |

Все экраны строятся по `rules/web-frontend.md` (shadcn-only, Plexor DS токены,
Material Symbols Rounded через `@/shared/ui/icon`, монохром + статусы). Дизайн-эталон вида — прототип `docs/design/`.

## Build & Test gate — текущее состояние

| Гейт | Команда | Статус |
|---|---|---|
| **Build** | `dotnet build plexor.slnx -c Debug` | ✅ 0 ошибок (compile + analyzers + format-check) |
| **Tests** | `dotnet test` (unit suites) | ✅ **308/311 passing + 2 skipped** (1 known fail: enum-description маппинг, в `BACKEND-ISSUES.md`; 2 skipped — NodeAgent image qcow2 paths под Linux-only path) |
| **Format-check** | `VerifyFormatOnBuild` (excludes Migrations/obj/bin/Generated/`*.g.cs`) | ✅ |
| **EF migrations** | `dotnet ef migrations add <Name>` (tool-only) | ✅ нет hand-edits |

Test suites breakdown (обновлено 2026-07-19):
- **Plexor.NodeAgent.Unit**: 75 passed + 2 skipped (новый suite: compute providers, runtime providers, image registry, lifecycle)
- Plexor.Modules.Clusters.Unit: 40/40 (24 базовых + 16 Tier 3-5 workload/controller тестов)
- Plexor.Modules.Sigil.Unit: 8/8
- Plexor.Shared.Capabilities.Unit: 7/7
- Plexor.Shared.Configuration.Unit: 21/21
- Plexor.Shared.Mtls.Unit: 4/4
- Plexor.Shared.Identifiers.Unit: 14/14
- Plexor.Shared.Filtering.Unit: 114/115 (enum-description pending)
- Plexor.Shared.Authorization.Unit: 17/17
- Plexor.Host.UnitTests: 8/8 (требует запущенного Postgres)
- **Total: 308 passed, 2 skipped, 1 failed (known)**

## Зафиксированные решения (прошлые, остаются актуальными)

- **K3s для managed k8s**, **ClusterSpec = node pools** (НЕ master/worker counts), **POST=create/PUT=update**, plain GET polling 2-3 sec, **UI/CLI peer**, schema-per-module, route `/k8s` (НЕ `/clusters` — тот уже занят fleet), kubeconfig retrieval обязателен в v0.1, **тема монохром-ink + статусные цвета** (НЕ фиолетовый бренд), **Onest + JetBrains Mono**, **Material Symbols Rounded** (Iconify), **Runtime/Binding модель** (Plexor = транслятор поверх готовых решений), **Binding = first-class объект (wiring + default-deny security)**.

## Resolved drift (что было починено)

1. **2026-07-08:** Топология (`information-architecture.md` → self-hosted top-level), бренд/тема (фиолетовый→монохром, Inter→Onest, Lucide→Material Symbols Rounded), добавлен `screens/00-clusters.md`.

2. **2026-07-16:** `STATE.md` — обновлена позиция с "❌ не начато" → "🟡 MVP-готов, Tier 3-5 остались". Реальность: Identity ✅, Clusters ✅, Workloads Tier 1+2 ✅, mTLS ✅, capabilities ✅.

3. **2026-07-19:** Phase D Tier 1-5 + 3.5 закрыты end-to-end. STATE.md синхронизирован:
   - Tier tracker: Tier 3 (Docker Compose), Tier 3.5 (LibvirtKvm/Lxc/Qemu reference impls),
     Tier 4 (Podman Quadlet + drift detection), Tier 5 (k3s provider + action endpoints) — ✅
   - Plexor.NodeAgent секция: расширена (compute abstractions, runtime providers, HttpImageRegistry,
     NodeAgent Dockerfile, action endpoints)
   - Cluster & Compute секция: Tier 3-5 + 3.5 переехали в ✅
   - Tests: `204/205 → 308/311 + 2 skipped`. Plexor.NodeAgent.Unit появился (75 + 2 skipped),
     Plexor.Modules.Clusters.Unit вырос 24 → 40 (+16 Tier 3-5 тестов)
   - Phase E0 завершён, Э6 → ✅, Э7 → ✅ (в рамках Э6)
   - Branches: `git branch -d plan/clusters` — завершённая worktree удалена,
     активны `plan/{auth-providers,k8s,mvp-deploy,runtime-providers}`

## Дизайн-система — два артефакта, одна истина

| Артефакт | Роль | Путь |
|---|---|---|
| **Прототип** (`styles.css` + `design-system.html` + screens/*.html) | Source of truth **как выглядит** | `.agents/docs/design/` |
| **Реализация** (shadcn primitives + `index.css` токены) | Source of truth **как в коде** | `web/apps/console/src/shared/ui/` + `src/index.css` |

`index.css` **зеркалят** `styles.css`. Меняешь палитру → правишь **оба**.

## Что читать (карта доков)

1. `.agents/STATE.md` — этот файл (позиция + этапы + решения).
2. `.agents/HANDOFF.md` — стиль общения + build-конвенции.
3. `docs/scope.md` — что в MVP.
4. `docs/architecture.md` (+ `architecture/networking.md`, `architecture/runtimes-and-bindings.md`, `architecture/concepts/runtime-capabilities-networking.md`) — слои, data flow, open architecture questions.
5. `docs/modules.md`, `docs/providers.md` — модули и провайдеры.
6. `docs/ui/README.md` → `information-architecture.md` → `screens/` — UI.
7. `rules/web-frontend.md` — правила фронта (обязательны).
8. `rules/coding/*` — C# conventions, anti-patterns, class decomposition.
9. `.planning/BACKEND-ISSUES.md` — зафиксированный тех-долг (enum-description mapping и др.).

## Открытые вопросы / next

### Phase D — что осталось (2026-07-19)

- **✅ Tier 3-5 + 3.5** закрыты. Workload loop реальный: control-plane state ↔ mTLS ↔
  runtime provider (Docker Compose / Podman Quadlet / k3s) ↔ drift detection ↔ action endpoints.
- **Следующий шаг — Phase D installer** (`plx init` end-to-end + node join + workload
  provisioning): Tier 3.5 reference impls (LibvirtKvm/Lxc/Qemu) ещё не подключены к
  installer — Docker Compose primary для v0.1.
- **Known sol-state drift** (не в scope этой правки): `.agents/plans/runtime-providers/PLAN.md`
  в статусе `ready`, без `SUMMARY.md`, хотя Tier 3-5 уже реализованы в git.
  Закрыть в отдельной задаче B (см. `.agents/skills/soly-framework`).

### Frontend
- Следующий экран после backend: **VM detail** (`screens/02-vm-detail.md`).
- `/clusters` list: primary-кнопка сейчас «Документация» — обсудить корректный primary CTA для self-hosted.
- OpenAPI-codegen: реальные контроллеры (`Plexor.Host.OpenApi`) → `artifacts/openapi.json` → kubb → фронт получает typed client, MSW можно убрать.

### Open architecture questions (`docs/architecture/concepts/runtime-capabilities-networking.md`)
- **Tier 2 (multi-tenant blockers):** N-3 tenant network isolation, N-4 VPC resource, B-1 binding-as-firewall.
- **Tier 3 (production):** N-1 Floating IP, N-2 edge gateway, N-6 admin VPN.
- **Tier 4:** N-5 service mesh.

### Соседние plans (отдельные worktrees)
- `plan-runtime-providers` — runtime abstraction (Docker/KVM/LXC).
- `plan-k8s` — managed Kubernetes как app provider.
- `plan-auth-providers` — dual auth-provider layer (Sigil + external OIDC per tenant).
- `plan-mvp-deploy` — manual OpenNebula smoke test.