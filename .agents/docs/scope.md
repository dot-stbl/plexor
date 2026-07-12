# Scope — MVP и расширяемость

## Принцип

**MVP-минимум, marketplace-архитектура позволяет расти динамически.**

Plexor стартует с 8-10 core сервисов в MVP. Новые приложения
добавляются через **app providers** (marketplace template), а
новые install backends — через код в Plexor (не плагин).

**Deploy topology**: MVP = один бинарь `Plexor.Host` (modular monolith).
Phase 2+ может вынести Audit, Telemetry, Network в отдельные бинарь
**по измеренному bottleneck'у**, не превентивно — extraction-ready
паттерн закладывается с первого дня, чтобы извлечение = deploy change,
не refactor. Стратегия, rationale, alternatives —
[ADR-0001](../../planning/adr/0001-selective-decomposition.md). Per-module
extraction tier — [modules.md §Extraction Tier](modules.md#extraction-tier).

## MVP — core сервисы (8-10)

| # | Ресурс | Модуль | Описание |
|---|--------|--------|----------|
| 1 | **Tenant** | Tenants | Изоляция верхнего уровня |
| 2 | **Project** | Tenants | Scope внутри тенанта |
| 3 | **User** + **Role** | Identity | IAM, RBAC, OAuth/OIDC |
| 4 | **SSH Key** | Identity | Публичные ключи для VM-доступа |
| 5 | **Virtual Machine** | Compute | VM lifecycle (built-in KVM/LXD) |
| 6 | **Volume** | Storage | Блочное хранилище (built-in Ceph RBD / local-lvm) |
| 7 | **Snapshot** | Storage | Снимки VM и volume |
| 8 | **Object Storage bucket** | Storage | S3-compatible (built-in Ceph RGW / MinIO) |
| 9 | **VPC** + **Subnet** + **Security Group** | Network | Изоляция сети (built-in OVS / Cilium) |
| 10 | **Floating IP** + **Load Balancer** | Network | Публичный IP и LB |
| + | **App Marketplace** | Marketplace | Каталог app providers + instances + lifecycle |
| + | **Metering + Invoice** | Billing | Usage метрики и счета (внутренний account) |

### Install providers (built-in code)

Выбираются при `plx init` или ISO install через system probes:

| Категория | Provider | Альтернативы | Когда выбирается |
|---|---|---|---|
| **Compute** | `kvm` (default) | lxd, pod, firecracker | Linux + есть /dev/kvm |
| **Network** | `ovs` (default) | cilium, host (bridge-only) | Multi-VM нужен overlay |
| **Storage (block)** | `ceph-rbd` (default) | local-lvm | Multi-node; replication нужна |
| **Storage (object)** | `ceph-rgw` (default) | minio | S3-compatible нужен; multi-node |
| **State DB** | `postgresql` (default) | — | Всегда |
| **Event bus** | `nats` (default) | — | Всегда |

User может override через `plexor.yaml`. Добавление новых install
providers = новый проект в солюшене, не NuGet-пакет.

### App providers (marketplace, MVP набор)

Встроенные examples (наши референсные templates) — пользователь может
установить сразу после установки Plexor:

| Provider | Описание |
|---|---|
| `postgresql` | Standalone Postgres 15+ |
| `redis` | Standalone Redis 7+ |
| `nginx` | Reverse proxy / ingress |
| `minio` | S3-compatible object storage (instance, не install) |
| `keycloak` | OAuth/OIDC server (для self-hosted identity) |
| `wordpress` | Popular CMS |
| `ghost` | Modern CMS |

**Внешние app providers** (от community) могут добавляться через
`plx provider install <source>` где source = git URL, OCI artifact,
или tarball. См. [providers.md](providers.md).

### Что НЕ в MVP (Phase 2+)

| Что | Когда | Как добавляется |
|---|---|---|
| Managed Kubernetes | Phase 2 | app provider `k8s-cluster` |
| Container Registry (Docker/OCI) | Phase 2 | app provider `harbor` |
| Backup / Recovery | Phase 2 | app provider `velero` или built-in snapshot schedule |
| Audit Trails (SIEM export) | Phase 3 | app provider `olfs` |
| Cloud DNS как сервис | Phase 3 | app provider `powerdns` |
| Bare-metal provisioning | Phase 3 | install provider `ironic` / `maas` |
| Multi-region failover | Phase 3 | postgres logical replication + BDR |
| CDN | out of scope | — |
| AI/ML services | out of scope | — |

## Расширяемость

### Новый core install provider (e.g. firecracker)

```
1. Добавляем проект src/providers/Plexor.Providers.Compute.Firecracker/
2. Реализуем IComputeProvider (built-in interface)
3. Регистрируем в Plexor.Host/Program.cs DI
4. Rebuild + redeploy
5. plx init detect /dev/kvm + jailer binary, score firecracker
6. User picks: plexor.yaml: install: { compute: firecracker }
```

Гарантии: ядро не знает о конкретных реализациях. Смена provider
= новый проект в солюшене + DI регистрация.

### Новый app provider (e.g. nginx)

```
1. Author создает git repo с provider.yaml:
   https://github.com/community/plexor-provider-nginx
2. Author добавляет в catalog.yaml (или публикует свой)
3. User: plx provider install https://github.com/community/plexor-provider-nginx
4. Plexor reads provider.yaml, валидирует config schema
5. User: plx provider install-instance nginx
6. Plexor deploys через Plexor.NodeAgent (runs install hooks)
```

**НЕ NuGet, НЕ plugin** — просто YAML template + shell-команды.
Plexor не зависит от .NET-версии provider'а.

## Что НЕ делаем (anti-scope)

- ❌ Конкуренция с AWS по ширине сервисов — мы curated subset.
- ❌ Собственный DNS-сервер (используем PowerDNS / CoreDNS upstream).
- ❌ Собственный IAM-сервер (используем Keycloak, Authentik, или Ory).
- ❌ Свой почтовый сервис, CDN, AI/ML.
- ❌ Multi-region failover из коробки (synchronous replication в платном
  tier, eventual consistency по умолчанию).
- ❌ Собственная разработка гипервизора, container runtime, ОС — берём
  готовые компоненты.
- ❌ App providers как .NET-сборки (только YAML templates).

Это **принципиальные решения**, не лень. Они экономят 95% работы и фокусируют
разработку на реальном value-add.

## Roadmap (ориентировочный)

| Квартал | Что |
|---------|-----|
| Q1 | installer (ISO + plx init) + install providers (KVM, Ceph, OVS, MinIO) + core modules (Tenants, Identity, Compute, Storage, Network) + Marketplace module с catalog/install workflow |
| Q2 | UI portal (TanStack Router + shadcn/ui) + 5-7 reference app providers (postgres, redis, nginx, minio, keycloak, wordpress, ghost) + billing + observability |
| Q3 | Managed K8s + Container Registry + Backup (app providers от community) |
| Q4 | Multi-region + Bare-metal provisioning + Audit Trails |

После Q4 — стабильный self-hosted облако уровня mid-tier SaaS.
