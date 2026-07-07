# Yandex Cloud → Plexor parity

Целевой scope — закрыть **8-10 самых важных YC-сервисов** в MVP, остальное
через provider-plugins в Phase 2+.

## ✅ В MVP (Plexor v0.x)

| YC сервис | Plexor equivalent | Provider | API endpoint |
|-----------|--------------------|----------|--------------|
| Resource Manager (tenants/folders) | `Plexor.Modules.Tenants` | built-in | `/api/v1/tenants`, `/api/v1/projects` |
| IAM (users, roles, service accounts) | `Plexor.Modules.Identity` | Keycloak | `/api/v1/auth/*`, `/api/v1/users/*` |
| Compute Cloud (VMs) | `Plexor.Modules.Compute` | KVM/LXD | `/api/v1/compute/vms` |
| Block Storage | `Plexor.Modules.Storage` (block) | Ceph RBD / local-lvm | `/api/v1/storage/volumes` |
| Object Storage | `Plexor.Modules.Storage` (object) | MinIO / Ceph RGW | `/api/v1/storage/buckets` |
| VPC (networks, subnets) | `Plexor.Modules.Network` | OVS / Cilium | `/api/v1/network/vpcs`, `/api/v1/network/subnets` |
| Security Groups | `Plexor.Modules.Network` | OVS + nftables | `/api/v1/network/security-groups` |
| Floating IPs | `Plexor.Modules.Network` | BGP (MetalLB) / NAT | `/api/v1/network/floating-ips` |
| Load Balancer | `Plexor.Modules.Network` | HAProxy | `/api/v1/network/load-balancers` |
| Snapshots | `Plexor.Modules.Storage` | Ceph / QCOW2 | `/api/v1/storage/snapshots` |
| Billing | `Plexor.Modules.Billing` | built-in | `/api/v1/billing/*` |

## 🟡 Phase 2 (post-MVP)

| YC сервис | Plexor equivalent | Provider | Effort |
|-----------|--------------------|----------|--------|
| Managed Kubernetes | `Plexor.Modules.Compute` (k8s feature) | K3s + Talos OS | 4 weeks |
| Managed PostgreSQL | **new `Plexor.Modules.Database`** | CloudNativePG | 3 weeks |
| Managed Redis | `Plexor.Modules.Database` | Spotahome Redis Operator | 2 weeks |
| Managed MySQL | `Plexor.Modules.Database` | CloudNativePG (also covers MySQL) | 2 weeks |
| Managed MongoDB | `Plexor.Modules.Database` | Percona operator | 3 weeks |
| Managed ClickHouse | `Plexor.Modules.Database` | Altinity operator | 3 weeks |
| Managed Kafka | `Plexor.Modules.Database` | Strimzi | 4 weeks |
| Container Registry | **new `Plexor.Modules.Registry`** | Harbor / Distribution | 4 weeks |
| Cloud DNS | `Plexor.Modules.Network` | PowerDNS | 3 weeks |
| API Gateway | `Plexor.Modules.Network` | YARP / Envoy | 4 weeks |
| Data Proc (Spark) | n/a | n/a | not planned |
| Data Transfer | n/a | n/a | not planned |

## 🟢 Phase 3 (long term)

| YC сервис | Plexor equivalent | Provider |
|-----------|--------------------|----------|
| Audit Trails (SIEM export) | `Plexor.Modules.Telemetry` (audit) | OLFS / clickhouse-export |
| Monitoring | `Plexor.Modules.Telemetry` | OpenTelemetry + Prometheus + Grafana |
| Logging | `Plexor.Modules.Telemetry` | OpenTelemetry Collector + Loki/OpenSearch |
| BareMetal | compute primitive | MAAS / Tinkerbell / Ironic |
| Backup | `Plexor.Providers.Storage.Velero` | Velero |
| CDN | n/a | not planned |
| Cloud Functions | `Plexor.Modules.Compute` (serverless) | Firecracker + NATS queue |
| SpeechKit / Vision / Translate | n/a | not planned |

## ❌ Out of scope

| YC сервис | Why |
|-----------|-----|
| Cloud CDN | Edge infrastructure (нужны PoP'ы, не делаем) |
| Cloud Armor (DDoS) | Edge infrastructure |
| Yandex 360 (mail, docs) | Не вписывается в self-hosted-облако |
| AI/ML сервисы | Разработка моделей — это отдельный бизнес |
| Cloud Games | Не вписывается |
| 1C/Garant | Специфика российского рынка, не для международного |

## Что мы НЕ копируем у YC (специфика self-hosted)

| YC | Plexor | Why иначе |
|----|--------|-----------|
| **Multi-AZ** (3+ зоны доступности) | Single-region MVP, multi-region в Phase 4 | Большинство self-hosted пользователей — одна ЦОД |
| **Global region** (ru-central1, eu-west-1) | Any single-region, через `plexor.yaml` | Self-hosted — что поставил, то и есть |
| **Interconnect (private links to on-prem)** | WireGuard для site-to-site | Проще и достаточно для SMB |
| **SpeechKit / Vision / Translate** | — | Не входит в scope |
| **Compliance certifications** (FSTEC, ISO) | Opt-in plugins | Это operational cert, не core |
| **Pricing per-second** (YC биллит поминутно) | Per-hour | Достаточно для SMB |

## Конкурентные преимущества Plexor vs YC

1. **Self-hosted** — данные не покидают on-prem
2. **Без vendor lock-in** — provider-plugin архитектура
3. **One-command install** — `plx init` (vs YC требует expert setup)
4. **Полная extensibility** — любой сервис добавляется через plugin
5. **Open source** — Apache 2.0 / коммерческая модель на support

## Конкурентные отличия YC от Plexor

1. YC — managed service, Plexor — self-hosted (нужны свои devops)
2. YC имеет 30+ сервисов, Plexor — curated subset
3. YC имеет compliance сертификаты
4. YC имеет мобильное приложение (Plexor — нет)
5. YC имеет Cloud Shell встроенный в UI (Plexor — `plx` CLI отдельно)

## Что должно быть в MVP по фичам (детально)

### Tenant + Project hierarchy
- YC: Organization → Folder → Project → Cloud → Resource
- Plexor: Tenant (org) → Project → Resource
- **Упрощение**: убираем Cloud level, Region — параметр кластера.

### VM lifecycle
- YC: создание → provisioning → running → stopped → deleted
- Plexor: те же + booting, error states с explict error codes
- **Упрощение**: нет preemptible (Yandex имеет Spot Instances)
- **Доп**: Console через noVNC, SSH key inject

### Block storage
- YC: диски SSD/HDD/Hybrid, network-ssd, network-hdd
- Plexor: type field с одним из `[ssd, hdd]` (Phase 2: tier enum)

### Object storage  
- YC: bucket versioning, CORS, lifecycle rules
- Plexor: bucket + versioning в Phase 2 (просто флаг пока)
- **Упрощение**: пока нет CORS / lifecycle

### Networking
- YC: VPC с subnets, security groups (stateful), route tables
- Plexor: то же + security groups (stateful по умолчанию, Phase 3: choice)

## См. также

- [scope.md](scope.md) — что входит и не входит в MVP
- [modules.md](modules.md) — детали каждого модуля
- [providers.md](providers.md) — каталог провайдеров