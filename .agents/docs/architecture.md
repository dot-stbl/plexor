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
│  │          Billing / Telemetry                                       │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                              │ NATS     │ PostgreSQL
                              ▼          ▼
┌────────────────────────┐  ┌────────────────────────┐
│  Plexor.NodeAgent #1   │  │  Plexor.NodeAgent #2   │ ... #N
│  (one per compute node)│  │  on each compute node  │
│  ┌──────────────────┐  │  ┌──────────────────┐  │
│  │ Task dispatcher  │  │  │ Task dispatcher  │  │
│  │ + Providers:     │  │  │ + Providers:     │  │
│  │   • KVM          │  │  │   • KVM          │  │
│  │   • Ceph RBD     │  │  │   • Ceph RBD     │  │
│  │   • OVS          │  │  │   • OVS          │  │
│  │   • MinIO mc     │  │  │   • MinIO mc     │  │
│  │   • CloudNativePG│  │  │   • Ceph RGW     │  │
│  └──────────────────┘  │  └──────────────────┘  │
└────────────────────────┘  └────────────────────────┘
            │       │                │       │
            ▼       ▼                ▼       ▼
        ┌─────┐ ┌─────┐         ┌─────┐ ┌─────┐
        │ KVM │ │Ceph │         │ KVM │ │Ceph │
        └─────┘ └─────┘         └─────┘ └─────┘
```

## Принципы

1. **Single control plane**, multiple data-plane nodes. Никакого
   consensus между нодами — control plane решает всё.

2. **DB-of-record** = PostgreSQL. Ядро пишет state в Postgres, node-агенты
   только исполняют команды и репортят обратно.

3. **Asynchronous by default**: control plane принимает запрос → пишет
   state-row → емитит NATS-event → возвращает 202 Accepted. UI получает
   финальный статус через WebSocket / SSE.

4. **Provider-pluggable**: каждое инфраструктурное решение (KVM, Ceph,
   OVS, Keycloak…) за plugin-ом. Меняешь провайдера — не меняешь ядро.

5. **Multi-tenant by default**: всё через `tenant_id`, JWT-claim
   пробрасывается через всю цепочку. Глобальные EF Core query filters.

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
- **Composition root** в `Plexor.Host/Program.cs` (через Scrutor +
  Reflection) собирает DI-граф из всех модулей.
- **OpenAPI source-generator** автоматически эмитит
  `artifacts/openapi.json` на каждом `dotnet build`.
- **Scalar** для API-документации (замена Swagger UI).

### 3. Data plane (Plexor.NodeAgent)
- **Worker Service** на каждом compute-ноде.
- **gRPC-client** к control plane с mTLS (mtls-ca из cluster bootstrap).
- **Локальные providers** собраны как DI-контейнер на ноде.
- **Health-check** loop отправляет состояние каждые 30s (CPU, RAM,
  диски, тенант-локальные VM counts).

### 4. Event bus
- **NATS JetStream** для command-events (control → node),
  status-events (node → control), и broadcast-events (broadcast).
- **Topic-based** routing: `plexor.compute.vm.create`, `plexor.network.lb.scaled`.
- **Durable subscriptions** для критичных events.
- **Decoupled** — node может перезагрузиться и catch-up missed events.

### 5. Persistence
- **PostgreSQL** для control-plane state:
  - `tenants`, `projects`, `users`, `roles`, `role_bindings`
  - `<resource>_spec`, `<resource>_status` (K8s-style pattern)
  - `audit_log` (append-only)
  - `metering` (hourly rollups)
  - `provider_catalog` (включая сторонние плагины)
- **Redis** для caches + distributed locks + rate-limiting
- **MinIO/Ceph RGW** для:
  - Snapshot artifacts (qcow2 диффы)
  - Object storage buckets (для пользователей)
  - Backup archives

### 6. Infrastructure layer (за провайдерами)
KVM, Ceph, OVS, MinIO, Keycloak, CloudNativePG и т.д. — каждый через свой
provider-plugin. См. [providers.md](providers.md).

## Data flow

### Provision VM

```
1. UI: POST /api/v1/compute/vms
   Body: { name, flavor, image, vpc, subnet, sg, user_data }
   Headers: X-Tenant-Id (or JWT)

2. Plexor.Host: 
   - auth middleware validates JWT, extracts tenant_id
   - Plexor.Modules.Compute.Application.CreateVmHandler:
       a. validates spec via FluentValidation
       b. calls scheduler (asks "which node?")
       c. writes vms_spec row + vms_status (phase=Provisioning)
       d. publishes "VmRequested" to NATS
   - returns 202 Accepted with VM id + Location header

3. Plexor.NodeAgent (subscribed to plexor.compute.*):
   - receives VmRequested event
   - calls Plexor.Providers.Compute.Kvm:
       a. libvirt define + start
       b. Plexor.Providers.Storage.Ceph: attach RBD volume
       c. Plexor.Providers.Network.Ovs: plug to OVS bridge on subnet
       d. cloud-init apply
   - publishes "VmRunning" event

4. Plexor.Host (subscribed to plexor.compute.vm.lifecycle):
   - updates vms_status (phase=Running, internal_ip, node_id)
   - updates metering counter (+1 vm_running)

5. UI (subscribed via SSE):
   - sees status change in TanStack Query cache
   - user sees VM appears as "Running"
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

- ❌ Plugin → ядро зависимости (только Plexor.Core.Providers).
- ❌ Module-A → Module-B.Infrastructure зависимости (только .Application или .Contracts).
- ❌ Прямой PostgreSQL доступ из data plane (только через control plane).
- ❌ Sync blocking calls в критичных path'ах (всё async + cancellation tokens).
- ❌ Hardcoded secrets (всё через Plexor.Shared.Configuration + Vault).

## Подробнее

- [modules.md](modules.md) — каждый модуль детально.
- [providers.md](providers.md) — как устроен provider SDK.
- [operations/install.md](operations/install.md) — install flow.
- [yandex-cloud-parity.md](yandex-cloud-parity.md) — feature parity map.