# Scope — MVP и расширяемость

## Принцип

**MVP-минимум, провайдер-архитектура позволяет расти динамически.**

Plexor стартует с 8-10 сервисов в MVP, но архитектура устроена так, что
любой новый ресурс добавляется через provider plugin без правок ядра.

## MVP — что входит (8-10 сервисов)

| # | Ресурс | Модуль | Описание |
|---|--------|--------|----------|
| 1 | **Tenant** | Tenants | Изоляция верхнего уровня |
| 2 | **Project** | Tenants | Scope внутри тенанта |
| 3 | **User** + **Role** | Identity | IAM, RBAC, OAuth/OIDC |
| 4 | **SSH Key** | Identity | Публичные ключи для VM-доступа |
| 5 | **Virtual Machine** | Compute | VM lifecycle (KVM, LXD, VMware, pod) |
| 6 | **Volume** | Storage | Блочное хранилище (Ceph RBD, ZFS, local-lvm) |
| 7 | **Snapshot** | Storage | Снимки VM и volume |
| 8 | **Object Storage bucket** | Storage | S3-compatible (MinIO, Ceph RGW) |
| 9 | **VPC** + **Subnet** + **Security Group** | Network | Изоляция сети |
| 10 | **Floating IP** + **Load Balancer** | Network | Публичный IP и LB |
| + | **Metering + Invoice** | Billing | Usage метрики и счета (внутренний account) |

### Что НЕ в MVP (Phase 2+)

| Ресурс | Когда | Как добавляется |
|--------|-------|-----------------|
| Managed PostgreSQL | Phase 2 | `Plexor.Providers.Database.CloudNativePg` |
| Managed Redis | Phase 2 | `Plexor.Providers.Database.SpotahomeRedis` |
| Managed Kubernetes | Phase 2 | `Plexor.Providers.Compute.K3sCluster` |
| Container Registry (Docker/OCI) | Phase 2 | `Plexor.Providers.Registry.Harbor` |
| Cloud DNS как сервис | Phase 3 | `Plexor.Providers.Network.PowerDns` |
| Audit Trails (SIEM export) | Phase 3 | `Plexor.Providers.Audit.Olfs` |
| Backup / Recovery | Phase 3 | `Plexor.Providers.Storage.Velero` |
| Bare-metal provisioning | Phase 3 | `Plexor.Providers.Compute.Maas` / `Tinkerbell` |
| CDN | out of scope | not planned |
| AI/ML services | out of scope | not planned |

## Расширяемость — provider-plugin model

### Как добавляется новый ресурс

```
1. Добавляем контракт в Plexor.Core.Providers:
       I<New>Provider : IResourceProvider<<New>, <New>Spec>
2. Добавляем NuSpec (минимальный installer)
3. Любой контрибьютор пишет реализацию:
       Plexor.Providers.<X>.<New>/<New>Provider.cs
   - живёт в отдельной сборке
   - ссылается на Plexor.Core.Providers
   - публикуется как NuGet пакет
4. Установка:
       plx provider install Plexor.Providers.Compute.Firecracker --version 0.1.0
   → кладеётся в /var/lib/plexor/providers/
   → перезапуск Plexor.Host
   → autodetect подхватывает через DI scan
5. Использование:
       plexor compute create --provider=firecracker ...
```

### Гарантии расширяемости

- **Ядро ничего не знает о конкретных реализациях** — только интерфейсы.
- **Provider-plugin пакеты не зависят от ядра** (кроме Plexor.Core.Providers).
- **DI и autodetect** через Scrutor + Marker-интерфейсы.
- **Capability-метаданные** в `ProviderInfo` позволяют resolver'у
  подставлять провайдер по feature-матрице.
- **Schema-versioning** — контракты версионируются, провайдер указывает
  `ImplementsContracts = ["v1", "v2"]`, ядро выбирает последний общий.

## Что НЕ делаем (anti-scope)

Список вещей, которые **сознательно не делаем** в Plexor:

- ❌ Конкуренция с AWS по ширине сервисов — мы curated subset.
- ❌ Собственный DNS-сервер (используем PowerDNS / CoreDNS upstream).
- ❌ Собственный IAM-сервер (используем Keycloak, Authentik, или Ory).
- ❌ Свой почтовый сервис, CDN, AI/ML.
- ❌ Multi-region failover из коробки (synchronous replication в платном
  tier, eventual consistency по умолчанию).
- ❌ Собственная разработка гипервизора, container runtime, ОС — берём
  готовые компоненты.

Это **принципиальные решения**, не лень. Они экономят 95% работы и фокусируют
разработку на реальном value-add.

## Roadmap (ориентировочный)

| Квартал | Что |
|---------|-----|
| Q1 | installer + autodetect + KVM/Ceph/OVS providers + 8-10 services MVP |
| Q2 | UI portal (TanStack Router + shadcn/ui) + billing + observability |
| Q3 | Managed K8s + Managed PostgreSQL + Container Registry |
| Q4 | Multi-region + Bare-metal provisioning + Audit Trails |

После Q4 — стабильный self-hosted облако уровня mid-tier SaaS.