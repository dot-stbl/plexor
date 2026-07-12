# STATE — canonical current position of Plexor

> **Read this FIRST.** Single source of truth: что построено, на каком этапе,
> какие решения зафиксированы, какой drift уже разрешён. Заменяет собой
> «текущее состояние» из `HANDOFF.md` (тот теперь — только про стиль общения
> и build-конвенции; состояние живёт здесь).
>
> Версия: 0.2.0 · обновлено: 2026-07-08

---

## Что строим

**Plexor** — self-hosted cloud platform (YC / Hetzner-класса) для команды `.stbl`.
Один control-plane (`Plexor.Host`) + N compute-нод (`Plexor.NodeAgent`), которые
присоединяются zero-touch через join-токен. Расширяемость — provider-plugin
(install providers built-in, app providers через marketplace-шаблоны).

Stack: **.NET 10** modular monolith + Vite/React портал. Подробнее — `docs/architecture.md`.

## Топология — self-hosted (КАНОН, решено 2026-07-08)

Plexor — **single-cluster self-hosted fleet**, НЕ Yandex-Cloud multi-tenant.

- Роуты портала **top-level**: `/vms`, `/clusters`, `/networks`, `/billing`, `/audit`.
  НЕ `/projects/{pid}/...`. VM и Cluster — параллельные first-class ресурсы
  (см. `rules/web-frontend.md` rule 51).
- **Cluster** = один `Plexor.Host` + ноды, вступившие по join-токену. Юзер сам
  ставит control-plane (ISO / `plx init`), UI отдаёт join-токены и команду
  `plx node join`. Networking — WireGuard mesh + VXLAN overlay + internal DNS
  (см. `docs/architecture/networking.md`).
- **Tenant / Project** остаются в домене/scope как уровни изоляции, но в MVP UI
  работает как **single-tenant**. Multi-tenant слой (project switcher, tenant
  admin, `/projects/{pid}/` префикс) — **Phase 2+**, не сейчас.

## Текущая позиция (честно)

| Слой | Состояние | Где |
|---|---|---|
| Фундамент (репо, build-гейты, .NET-скелет, правила) | ✅ готово | `plexor.slnx`, `.agents/rules/` |
| Дизайн-прототип (6 экранов HTML + styleguide + launcher) | ✅ готово | `.agents/docs/design/` |
| Портал (React, на MSW-моках) | 🟡 в работе | `web/apps/console/` |
| API / domain (.NET) | ❌ не начато | фронт мокается (kubb + MSW) |
| Installer (`plx init`) + node join | ❌ не начато | описано в `docs/operations/`, `architecture/networking.md` |

**Ключевое:** бэкенда на .NET ещё нет. Фронт строится против моков (kubb-codegen
из `artifacts/openapi.json` + MSW handlers, seed=1337). Контракты выводятся из
экранов, домен — из контрактов.

## Глобальные этапы (продукт)

| # | Этап | Статус |
|---|---|---|
| Э0 | Фундамент: репо, build-гейты, .NET-структура, правила, дизайн-система | ✅ |
| **Э1** | **UI-портал на моках (экраны + shadcn DS + kubb/MSW)** | **🟡 ← здесь** |
| Э2 | OpenAPI-контракты, выведенные из экранов → kubb-codegen | ⏳ |
| Э3 | Domain model + первый модуль **Compute** (.NET) | ⏳ |
| Э4 | Installer (`plx init`) + install providers + node join | ⏳ |
| Э5 | Остальные модули: Network, Storage, Identity, Billing, Marketplace | ⏳ |
| Э6 | `Plexor.NodeAgent` / data plane (реальное исполнение) | ⏳ |

## UI — экраны (порядок и статус)

Приоритет по персонам — см. `docs/ui/personas.md`. Брифы — `docs/ui/screens/`.

| Экран | Бриф | Код | Статус |
|---|---|---|---|
| VM list | `screens/01-vm-list.md` | `/vms` | ✅ построен, итерируется |
| Clusters (list + detail) | `screens/00-clusters.md` | `/clusters`, `/clusters/$id` | ✅ построен (self-hosted, добавлен вне исходных брифов) |
| Create VM | `screens/03-create-vm-wizard.md` | `/vms/new` | ✅ построен (пока страница, не multi-step wizard) |
| VM detail | `screens/02-vm-detail.md` | — | ⏭ следующий |
| Networks / VPC | `screens/04-network-vpc.md` | `/networks` (заглушка) | ⏳ |
| Billing / Usage | `screens/05-billing-usage.md` | `/billing` (заглушка) | ⏳ |
| Audit log | `screens/06-audit-log.md` | `/audit` (заглушка) | ⏳ |
| Storage (Volumes/Buckets) | — | — | ⏳ |
| IAM (Users/SSH/API keys) | `screens/99-future-screens.md` | — | ⏳ |
| Marketplace | `docs/ui/ui-inventory.md §7` | — | ⏳ |

Все экраны строятся по `rules/web-frontend.md` (shadcn-only, Plexor DS токены,
Material Symbols Rounded через `@/shared/ui/icon`, монохром + статусы). Дизайн-эталон вида — прототип `docs/design/`.

## Зафиксированные решения (НЕ пересматривать без запроса)

Продуктовые / стек — из `HANDOFF.md` (актуальны):
- Продукт **Plexor**, CLI-префикс **`plx`** (NativeAOT), **.NET 10**.
- **Modular monolith + node agents** (НЕ микросервисы).
- Provider pattern: `IProvider` + `I<Resource>Provider`; app providers = YAML-шаблоны (НЕ NuGet/plugin).
- DB **PostgreSQL** + EF Core 10 + Dapper; bus **NATS**; mapper **Riok.Mapperly**; validation **FluentValidation**; API-docs **Scalar**.
- UI: **Vite + React + TS + shadcn/ui (на Base UI) + Tailwind v4 + TanStack Router/Query + Zustand**; codegen **kubb**.

Новые (решено 2026-07-08):
- **Топология self-hosted** — канон (см. выше). Multi-tenant → Phase 2+.
- **K8s runtime = K3s** (НЕ kubeadm, НЕ Talos, НЕ kubespray). K3s в FE уже
  зашит (`v1.31.1+k3s1`, datastore etcd/sql, Traefik, Flannel, local-path);
  «Plexor — транслятор поверх готовых решений» совпадает с K3s
  single-binary pattern (8 компонент в коробке: Flannel, CoreDNS, Traefik,
  ServiceLB, NetworkPolicy, local-path, containerd, MetricsServer).
  **kubeadm отвергнут** — ручная сборка apt + cloud-init + manual kubelet
  config = максимум surface для автоматизации, противоположно цели.
  **Talos отложен** на v0.2+ как OS-уровневая альтернатива; K3s-bundle
  на Ubuntu для v0.1. **RKE2** = v0.2 hardening upgrade (CIS, FIPS, PSA).
- **ClusterSpec = node pools, НЕ master/worker counts.** Модель DOKS/GKE:
  массив `nodePools[]` с name/count/runtime(vm|lxc|bare)/cpu/ram/disk.
  Это уже в FE-форме `/k8s/new.tsx` (RepeatableRows). Принцип:
  UI = curated projection полной спеки, не плоский список полей.
- **POST = create (201/409), PUT = update.** НЕ POST-as-upsert. +
  `Idempotency-Key` header для safe retry (Stripe/Increase pattern).
  `plx k8s apply -f` мапится на `GET → POST/PUT`, не на отдельный verb.
- **Plain GET polling** каждые 2-3 сек для cluster state. UI:
  `phase` + `conditions` (K8s-стиль). НЕ WebSocket. v0.2+: SSE для
  event log. Long-running operation: 202 Accepted + status.
- **UI/CLI — peer, не UI-primary/CLI-secondary.** Оба = full-featured
  клиента к одному REST API. CLI = GitOps escape hatch (Rancher pattern).
- **Schema-per-module в одной PostgreSQL базе (Architecture theme):**
  `sigil` (identity), `realm` (tenants), `ledger` (billing),
  `atlas` (audit), `forge` (clusters), `outpost` (nodes/workloads),
  `shard` (workloads/details). Один PostgreSQL кластер на plexus,
  разные schema per модуль — как OpenStack (nova/neutron/cinder
  schemas в одном PG). Один connection string, один backup, одна
  HA-конфигурация; schemas дают изоляцию таблиц per bounded
  context. EF Core migrations per schema (свой
  `__EFMigrationsHistory` на каждый). v0.1 работает in-memory,
  БД приходит в Phase 1 с конкретными DbContext-ами на модуль —
  schema имена уже зафиксированы.
- **Route = `/k8s`** (НЕ `/clusters` — `/clusters` уже занят fleet/
  PlexorCluster'ом). Cluster и fleet — параллельные first-class ресурсы.
- **kubeconfig retrieval** (`GET /k8s/{name}/kubeconfig`) — обязателен
  в v0.1, без него кластер unusable. Server-side token generation,
  client получает готово.
- **Тема console — монохром-ink + статусные цвета**, НЕ фиолетовый бренд. Шрифт
  **Onest** (UI) + **JetBrains Mono** (данные), UI-иконки **Material Symbols Rounded**
  (Iconify, `@/shared/ui/icon`), цветные тех-логотипы — `<TechIcon>`. Осознанно
  расходится со старым `brand.md`; см. «Resolved drift».
- **Runtime/Binding-модель** (продуктовое ядро «облако — это легко»): Plexor —
  **транслятор поверх готовых решений** (тупой про деплой, умный про связывание).
  Service (что) отвязан от Runtime (где: VM/LXC/Docker/k8s). Рантаймы **direct**
  (Plexor владеет) vs **delegated** (k8s — чужой оркестратор, свой namespace).
  Манифест — тонкий конверт (модель B, Nomad-style), НЕ универсальный DSL
  (модель A / Crossplane). **Binding** — объект первого класса = wiring + модель
  безопасности (default-deny). Полностью: `docs/architecture/runtimes-and-bindings.md`.

## Resolved drift (что было починено 2026-07-08)

Документация местами отставала от кода/решений. Приведено к реальности:

1. **Топология.** `information-architecture.md` был про YC multi-tenant
   (`/projects/{pid}/`). → Переписан под self-hosted top-level роуты + node join.
2. **Бренд/тема.** `brand.md` / `ui.md` документировали фиолетовый `#5E5BE8` +
   Inter + Lucide. → Переписаны под монохром + Onest + JetBrains Mono. Иконки:
   Lucide→Phosphor→**Material Symbols Rounded** (Iconify, `@/shared/ui/icon`);
   Phosphor удалён. Старая палитра помечена deprecated.
3. **Clusters/Nodes отсутствовали в брифах** хотя это центр self-hosted. →
   Добавлен `screens/00-clusters.md`.

## Дизайн-система — два артефакта, одна истина

| Артефакт | Роль | Путь |
|---|---|---|
| **Прототип** (`styles.css` + `design-system.html` + screens/*.html) | Source of truth **как выглядит** (эталон вида, палитра, плотность) | `.agents/docs/design/` |
| **Реализация** (shadcn primitives + `index.css` токены) | Source of truth **как в коде** (компоненты, которые реально шипятся) | `web/apps/console/src/shared/ui/` + `src/index.css` |

Токены `index.css` **зеркалят** `styles.css` (там прямо написано `Source of
truth: .agents/docs/design/styles.css`). Меняешь палитру → правишь **оба**.
Правило: прототип задаёт вид, `web-frontend.md` задаёт как это собрать из shadcn.

## Что читать (карта доков)

1. `.agents/STATE.md` — этот файл (позиция + этапы + решения).
2. `.agents/HANDOFF.md` — стиль общения + build-конвенции.
3. `docs/scope.md` — что в MVP.
4. `docs/architecture.md` (+ `architecture/networking.md` — mesh; `architecture/runtimes-and-bindings.md` — Runtime/Binding-модель) — слои, data flow.
5. `docs/modules.md`, `docs/providers.md` — модули и провайдеры.
6. `docs/ui/README.md` → `information-architecture.md` → `screens/` — UI.
7. `rules/web-frontend.md` — правила фронта (обязательны).

## Открытые вопросы / next

- Следующий экран: **VM detail** (`screens/02-vm-detail.md`).
- UX-решения, требующие выбора — `docs/ui/ui-inventory.md §17` (пройти отдельно).
- `/clusters` list: primary-кнопка сейчас «Документация» — обсудить корректный
  primary CTA для self-hosted (регистрация control-plane vs docs).
- Э2: зафиксировать OpenAPI-контракты по построенным экранам (vms уже есть в kubb).
