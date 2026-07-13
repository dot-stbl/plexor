# Architecture — слои, потоки данных, границы

## TL;DR

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Browser / CLI / Terraform / Pulumi                                     │
└──────────────────────────────────────────────────────────────────────────┘
                                    │ HTTPS
                                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│  Plexor.Host  (control plane, single binary)                            │
│  ┌────────────┐  ┌─────────────┐  ┌─────────────┐  ┌──────────────────┐  │
│  │ REST API   │  │  gRPC API   │  │  Event Bus  │  │  Module DI       │  │
│  │ minimalAPI │  │  Server     │  │  NATS pub   │  │  composition     │  │
│  └────────────┘  └─────────────┘  └─────────────┘  └──────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Modules: Compute / Network / Storage / Identity / Tenants /        │  │
│  │          Billing / Telemetry / Marketplace                        │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                              │ NATS     │ PostgreSQL
                              ▼          ▼
┌────────────────────────┐  ┌────────────────────────┐
│  Plexor.NodeAgent #1   │  │  Plexor.NodeAgent #2   │ ... #N
│  (one per compute node)│  │  (one per compute node)│
│  ┌──────────────────┐  │  ┌──────────────────┐  │
│  │ Task dispatcher  │  │  │ Task dispatcher  │  │
│  │ (runs install     │  │  │ (runs install     │  │
│  │  provider steps   │  │  │  provider steps   │  │
│  │  on this node)   │  │  │  on this node)   │  │
│  └──────────────────┘  │  └──────────────────┘  │
└────────────────────────┘  └────────────────────────┘
            │       │                │       │
            ▼       ▼                ▼       ▼
        ┌─────┐ ┌─────┐         ┌─────┐ ┌─────┐
        │ KVM │ │Ceph │         │ KVM │ │Ceph │
        └─────┘ └─────┘         └─────┘ └─────┘
```

**Install providers** (KVM, Ceph, OVS, MinIO) живут в `Plexor.Host` (built-in code). Выбираются при инсталляции.

**App providers** (WordPress, Postgres, custom apps) живут в Plexor.Modules.Marketplace, загружаются из marketplace (git/OCI/tarball, **не NuGet**).

## Принципы

1. **Single control plane**, multiple data-plane nodes. Никакого
   consensus между нодами — control plane решает всё.

2. **DB-of-record** = PostgreSQL. Ядро пишет state в Postgres, node-агенты
   только исполняют команды и репортят обратно.

3. **Asynchronous by default**: control plane принимает запрос → пишет
   state-row → емитит NATS-event → возвращает 202 Accepted. UI получает
   финальный статус через WebSocket / SSE.

4. **Install providers (built-in)** — выбор инфраструктуры при
   инсталляции (Ceph vs MinIO, OVS vs Cilium, KVM vs LXD). Код в Plexor,
   выбирается через `plx init` probes + user override в plexor.yaml.

5. **App providers (marketplace)** — шаблоны развертывания приложений
   (WordPress, Postgres, custom). YAML spec, distributed via git/OCI/tarball.
   НЕ NuGet, НЕ plugin — просто декларативное описание.

6. **Multi-tenant by default**: всё через `tenant_id`, JWT-claim
   пробрасывается через всю цепочку. Глобальные EF Core query filters.

## Decomposition strategy

**MVP**: modular monolith — все модули в `Plexor.Host`.

**Phase 2+**: planned extraction трёх модулей в отдельные бинарь по
измеренному bottleneck'у (Audit, Telemetry, Network — детали в
[modules.md §Extraction Tier](modules.md#extraction-tier)).

Не OpenStack-style 30+ сервисов: operational overhead (+1 deploy, +1
CI pipeline, +1 schema migration, +1 monitoring target) не оправдан
для middleware-облака. Counter-evidence: GitHub, Shopify, Stack
Overflow обслуживали миллиарды запросов на монолите.

**Extraction-ready by design** — извлечение = deploy change, не
refactor. Module contracts в `Plexor.Shared.Contracts`, cross-module
async через outbox events, per-module DbContext+schema.

Подробности и rationale — [ADR-0001](../../planning/adr/0001-selective-decomposition.md).

## Слои

### 1. Presentation
- **Web UI** (`web/apps/console`) — Vite + React + shadcn/ui + TanStack
  Router. SPA, деплой через nginx в Plexor.Host (StaticFiles).
- **CLI** (`plx`) — NativeAOT binary, те же endpoints что UI.

### 2. Control plane (Plexor.Host)
- **ASP.NET Core 10** с minimal APIs
- **Modules**: каждый модуль — три проекта (Domain / Application /
  Infrastructure). Domain — нет зависимостей, Application — на shared,
  Infrastructure — на Application + EF Core + telemetry.
- **Install providers** (KVM, Ceph, OVS, MinIO) — **built-in code**
  в Plexor.Host, выбираются при `plx init` через probes.
- **Composition root** в `Plexor.Host/Program.cs` (через Scrutor +
  Reflection) собирает DI-граф из всех модулей.
- **OpenAPI source-generator** автоматически эмитит
  `artifacts/openapi.json` на каждом `dotnet build`.
- **Scalar** для API-документации (замена Swagger UI).

### 3. Data plane (Plexor.NodeAgent)
- **Worker Service** на каждом compute-ноде.
- **gRPC-client** к control plane с mTLS (mtls-ca из cluster bootstrap).
- **App provider executor** — NodeAgent получает NATS event
  "provider.install" с shell-командами, запускает их на своей ноде.
  Каждый provider сам решает, на каких нодах работать (через labels/affinity).
- **Health-check** loop отправляет состояние каждые 30s (CPU, RAM,
  диски, тенант-локальные VM counts).

### 4. Event bus
- **NATS JetStream** для command-events (control → node),
  status-events (node → control), и broadcast-events (broadcast).
- **Topic-based** routing: `plexor.compute.vm.create`, `plexor.app.deploy`,
  `plexor.network.lb.scaled`.
- **Durable subscriptions** для критичных events.
- **Decoupled** — node может перезагрузиться и catch-up missed events.

### 5. Persistence
- **PostgreSQL** для control-plane state:
  - `tenants`, `projects`, `users`, `roles`, `role_bindings`
  - `<resource>_spec`, `<resource>_status` (K8s-style pattern)
  - `app_providers` (catalog of installed app providers)
  - `provider_instances` (running app deployments)
  - `audit_log` (append-only)
  - `metering` (hourly rollups)
- **Redis** для caches + distributed locks + rate-limiting
- **MinIO/Ceph RGW** для:
  - Snapshot artifacts (qcow2 диффы)
  - Object storage buckets (для пользователей)
  - Backup archives
  - **App provider source archives** (git/OCI/tarball cache)

### 6. Marketplace (app providers)

App providers НЕ являются .NET-плагинами. Это декларации в YAML
(как Helm chart). Plexor интерпретирует их и применяет на нужных нодах.

Подробнее: [providers.md](providers.md) (App providers section) и
[modules.md](modules.md) (Marketplace module).

## Data flow

### Install Plexor (ISO / plx init)

```
1. User boots ISO (or runs plx init on Ubuntu)
2. Installer runs SystemProbe:
   - /dev/kvm exists? libvirtd? VT-x?
   - openvswitch running? cilium kernel support?
   - ceph-mon running? OSDs available?
   - postgresql, nats available?
3. User picks (or accepts defaults):
   plexor.yaml: install: { compute: kvm, network: ovs, storage: ceph }
4. Installer configures /etc/plexor/install.yaml
5. Plexor.Host starts with selected install providers
```

### Deploy app via marketplace

```
1. UI: Marketplace → browse app providers
   (loaded from local catalog or remote registry)

2. UI: pick provider (e.g. wordpress 0.2.0)
   - shows resources required, config schema

3. UI: install-instance
   POST /api/v1/marketplace/instances
   Body: { provider: 'wordpress', version: '0.2.0', config: { siteTitle: ... } }

4. Plexor.Host:
   - Plexor.Modules.Marketplace.Application.InstallInstanceHandler:
       a. validate config against provider schema
       b. resolve dependencies (postgresql if required)
       c. select target node (scheduler)
       d. allocate resources (volume, floating IP, etc.)
       e. write provider_instances row (status=installing)
       f. publish "provider.install" to NATS
   - returns 202 Accepted with instance id

5. Plexor.NodeAgent (subscribed to plexor.app.install):
   - receives event with provider spec + instance id
   - pulls provider commands (git clone, helm install, podman run, etc.)
   - executes install hooks from provider.yaml
   - publishes "provider.installed" with status

6. Plexor.Host (subscribed to plexor.app.lifecycle):
   - updates provider_instances.status = running
   - stores instance metadata (URLs, credentials)

7. UI: see running app in instances list
```

### Provision VM (still works as before)

```
1. UI: POST /api/v1/compute/vms
2. Plexor.Host: writes spec + status, emits NATS event
3. Plexor.NodeAgent: libvirt define + start (built-in KVM install provider)
4. status event back to Host, update status
```

### User auth
```
1. UI: GET /.well-known/openid-configuration (Keycloak discovery)
2. UI: PKCE flow → token
3. UI: every API call: Authorization: Bearer <jwt>
4. Plexor.Host: JWT middleware validates signature against Keycloak JWKS
5. tenant_id, project_id, roles из claims → DI scope
```

## Что НЕ допускается

- ❌ App providers как .NET-сборки (НЕ plugin model, marketplace template).
- ❌ Module-A → Module-B.Infrastructure зависимости (только .Application или .Contracts).
- ❌ Прямой PostgreSQL доступ из data plane (только через control plane).
- ❌ Sync blocking calls в критичных path'ах (всё async + cancellation tokens).
- ❌ Hardcoded secrets (всё через Plexor.Shared.Configuration + Vault).
- ❌ NuGet для app providers (только git / OCI / tarball).

## Подробнее

- [modules.md](modules.md) — каждый модуль детально.
- [providers.md](providers.md) — install providers (built-in) + app providers (marketplace).
- [operations/install.md](operations/install.md) — install flow.
- [yandex-cloud-parity.md](yandex-cloud-parity.md) — feature parity map.
