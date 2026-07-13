# Modules — спецификация каждого модуля

Пlexor использует **modular monolith + plugin providers** архитектуру.
Каждый модуль — три проекта (.Domain / .Application / .Infrastructure),
живущих в `src/modules/Plexor.Modules.<Name>/`.

## Слои модуля (применимо ко всем)

```
Plexor.Modules.<Name>.Domain/         ← pure C#, no infra
   ├── Entities/                       domain aggregates (sealed, records)
   ├── ValueObjects/                   strongly-typed IDs, configs
   ├── Events/                         <Name>DomainEvent : IDomainEvent
   └── Errors/                         DomainError, ErrorCode, Result<T>

Plexor.Modules.<Name>.Application/    ← orchestration, no DB
   ├── Endpoints/                      minimal-API IEndpointRouteBuilder extensions
   ├── Installers/                     IServicesInstaller / IEndpointInstaller
   ├── Handlers/                       ICommandHandler<T>, IQueryHandler<T>
   ├── Models/                         Request/Response DTOs, validators
   ├── Security/                       IResourceAuthorizer<TResource>
   ├── Persistence/                    IRepository<T>, IReadOnlyRepository<T>
   └── Workers/                        (optional) IHostedService for the module

Plexor.Modules.<Name>.Infrastructure/ ← DB and infra
   ├── Persistence/                    EfRepository<T>, DbContext, configurations
   ├── Migrations/                     EF Core migrations
   ├── Installers/                     registration of EF types, hosted services
   └── Options/                        IOptions<T> record holders
```

## Список модулей

| Модуль | Что внутри | Зависит от |
|--------|-----------|-------------|
| `Plexor.Modules.Tenants` | Tenant + Project hierarchy | Identity (read) |
| `Plexor.Modules.Identity` | User, Role, IAM token, SSH key | Shared |
| `Plexor.Modules.Compute` | Virtual Machine, Snapshot-of-VM | Network, Storage, Identity |
| `Plexor.Modules.Network` | VPC, Subnet, SG, Floating IP, LB, DNS | Shared |
| `Plexor.Modules.Storage` | Volume, Snapshot-of-Volume, Bucket | Shared |
| `Plexor.Modules.Billing` | Metering record, Invoice | Tenants (read), Identity (read) |
| `Plexor.Modules.Telemetry` | Logs, metrics, audit event collector | Shared |
| `Plexor.Modules.Marketplace` | App provider catalog, instance lifecycle | Compute, Network, Storage, Identity |

Phase 2+:
- Managed PostgreSQL / Redis = app providers (не модули)
- Managed Kubernetes = app provider (`k8s-cluster`)
- Container Registry = app provider (`harbor`)
- Bare-metal provisioning = install provider (`ironic` / `maas`)

## Extraction Tier

Три модуля разрабатываются **extraction-ready** — в Phase 2+ могут быть
вынесены в отдельные бинарь без refactor:

- **Audit** → `Plexor.Audit.Host` (сейчас sub-concern модуля Telemetry)
- **Telemetry** → `Plexor.Telemetry.Host`
- **Network** → `Plexor.Network.Host`

Триггеры для extraction уточняются по измеренной нагрузке в момент
принятия решения, не заранее.

Остальные модули (Tenants, Identity, Compute, Storage, Billing,
Marketplace) остаются в монолите в обозримом roadmap.

Стратегия и rationale — [ADR-0001](../../planning/adr/0001-selective-decomposition.md).

## Plexor.Modules.Tenants

**Контракт верхнего уровня.** Tenant = организация, Project = scope
внутри тенанта (аналог folder в YC).

### Domain
- `Tenant { Id, Name, Slug, CreatedAt, Status }`
- `Project { Id, TenantId, Name, Slug, Quotas, CreatedAt }`
- События: `TenantCreated`, `ProjectCreated`, `QuotaExceeded`

### Application endpoints
- `POST   /api/v1/tenants`           create tenant (admin only)
- `GET    /api/v1/tenants`           list tenants (caller can see only own)
- `GET    /api/v1/tenants/{id}`      get tenant
- `POST   /api/v1/tenants/{id}/projects`   create project under tenant
- `GET    /api/v1/tenants/{id}/projects`   list projects

### Quotas
Tenant-level: `max_cpus`, `max_ram_mb`, `max_volumes`, `max_buckets`
Project-level: same but per-project.

Metering handler (через NATS) инкрементит current-usage, проверяет quota
перед каждой ресурсной операцией.

## Plexor.Modules.Identity

**IAM, RBAC, OAuth/OIDC.** Использует Keycloak как внешний IdP (или local
режим для dev/MVP).

### Domain
- `User { Id, TenantId, Email, DisplayName, Status }`
- `Role { Id, TenantId, Name, Permissions }`
- `RoleBinding { Id, UserId, RoleId, ProjectId? }` (subject → role, optionally scoped to project)
- `SshKey { Id, TenantId, UserId, Fingerprint, PublicKey }`
- `ApiKey { Id, TenantId, UserId, HashedSecret, ExpiresAt }` (service accounts)

### Application endpoints
- `POST   /api/v1/auth/login`         (OIDC code flow → Keycloak)
- `GET    /api/v1/auth/me`            current user info
- `POST   /api/v1/users`              (admin)
- `POST   /api/v1/users/{id}/ssh-keys`
- `POST   /api/v1/users/{id}/api-keys`
- `POST   /api/v1/roles`              (admin)
- `POST   /api/v1/role-bindings`

### Authorization model
- **Tenant-scoped claims**: `tenant_id` обязателен
- **Role-based**: permissions — строки вида `compute.vms.create`, `network.lb.delete`
- **Project scoping**: role binding может быть project-scoped

```
Permission hierarchy:
  compute.vms.create       (single VM)
  compute.vms.create.bulk  (≥ 5 VMs in one request — requires higher role)
  compute.vms.*            (all VM operations)
  *                         (super-admin)
```

## Plexor.Modules.Compute

**VM lifecycle, snapshots, scheduling.**

### Domain
- `VirtualMachine { Id, TenantId, ProjectId, Spec, Status, NodeId, ... }`
- `VmSpec { FlavorId, ImageId, VpcId, SubnetId, SgIds, UserData, ... }`
- `VmStatus { Phase (Pending/Provisioning/Running/Stopped/Error), ... }`
- `Flavor { Id, Cpus, MemoryMb, DiskGb, GpuType?, ... }`

### Application endpoints
- `GET    /api/v1/flavors`           list VM sizes
- `GET    /api/v1/images`            list OS images
- `POST   /api/v1/compute/vms`       create VM
- `GET    /api/v1/compute/vms`       list (filtered by tenant/project)
- `GET    /api/v1/compute/vms/{id}`  get
- `PATCH  /api/v1/compute/vms/{id}`  resize / update
- `POST   /api/v1/compute/vms/{id}/start`
- `POST   /api/v1/compute/vms/{id}/stop`
- `POST   /api/v1/compute/vms/{id}/reboot`
- `DELETE /api/v1/compute/vms/{id}`  terminate
- `POST   /api/v1/compute/vms/{id}/console`  → noVNC websocket URL

### Scheduler
- **Bin-packing first-fit** по умолчанию
- **Spread** опция (для HA)
- **GPU-aware** если flavor содержит GPU
- **Anti-affinity rules** для HA групп

### Phase 2: K3s cluster as compute primitive
- `POST   /api/v1/compute/k8s-clusters`
- рабочий K3s-кластер в нодах через Plexor.Providers.Compute.K3sCluster

## Plexor.Modules.Network

**VPC, subnets, security groups, LB, floating IPs, DNS.**

### Domain
- `Vpc { Id, TenantId, ProjectId, Cidr, Name, ... }`
- `Subnet { Id, VpcId, Cidr, Region, Zone }`
- `SecurityGroup { Id, VpcId, Name, IngressRules[], EgressRules[] }`
- `FloatingIp { Id, TenantId, PublicIp, AttachedToVmId?, Status }`
- `LoadBalancer { Id, VpcId, SubnetId, FrontendPort, BackendVms[], Status }`
- `DnsZone { Id, TenantId, Name, Records[] }`

### Application endpoints
- `POST   /api/v1/network/vpcs`
- `POST   /api/v1/network/vpcs/{id}/subnets`
- `POST   /api/v1/network/vpcs/{id}/security-groups`
- `POST   /api/v1/network/floating-ips`
- `POST   /api/v1/network/floating-ips/{id}/attach`
- `POST   /api/v1/network/load-balancers`
- `POST   /api/v1/network/dns-zones`

### Providers
- `Plexor.Providers.Network.Ovs` — OVS на compute-нодах
- `Plexor.Providers.Network.HaProxy` — load balancer
- `Plexor.Providers.Network.Nftables` — security groups через nftables

## Plexor.Modules.Storage

**Volumes (block), buckets (object), snapshots.**

### Domain
- `Volume { Id, TenantId, ProjectId, SizeGb, Type (ssd/hdd), Status, AttachedToVmId?, ... }`
- `Snapshot { Id, SourceId, SourceType (vm/volume), SizeGb, CreatedAt }`
- `Bucket { Id, TenantId, ProjectId, Name, Region, VersioningEnabled, ... }`

### Application endpoints
- `POST   /api/v1/storage/volumes`
- `POST   /api/v1/storage/volumes/{id}/attach`
- `POST   /api/v1/storage/snapshots`
- `POST   /api/v1/storage/snapshots/{id}/restore`
- `POST   /api/v1/storage/buckets`
- `GET    /api/v1/storage/buckets/{id}/objects`

### Providers
- `Plexor.Providers.Storage.Ceph` — Ceph RBD + RGW
- `Plexor.Providers.Storage.MinIo` — single-node S3
- `Plexor.Providers.Storage.Zfs` — ZFS over iSCSI

## Plexor.Modules.Billing

**Usage metering + invoice generation.**

### Domain
- `MeteringRecord { Id, TenantId, ProjectId, ResourceType, ResourceId, Metric, Value, PeriodStart, PeriodEnd }`
- `Invoice { Id, TenantId, PeriodStart, PeriodEnd, LineItems[], Total, Status }`

### How it works
- NATS-subscribers во всех модулях эмитят `metering.used` events с
  каждым state transition (vm.created, vm.running-hour, gb-stored-hour).
- `MeteringAggregator` (BackgroundService) раз в час делает rollups в
  `metering_hourly` table.
- `InvoiceGenerator` (cron) генерирует invoice за прошлый месяц.

### Pricing model
- Per-resource pricing (например, `vm.small = 0.01 USD/hour`)
- Pricing rules в `plexor.yaml` cluster spec
- Tenant может иметь custom pricing (entitled discounts)

## Plexor.Modules.Telemetry

**Логи, метрики, audit-события.**

### Application endpoints
- `GET    /api/v1/telemetry/logs`     structured log query (filter by tenant, time, resource)
- `GET    /api/v1/telemetry/metrics`  Prometheus-compatible scrape
- `GET    /api/v1/telemetry/audit`    audit log query (admin only)

### Audit
- Каждое мутирующее действие (через middleware) пишет
  `audit_log(actor_id, tenant_id, action, resource_type, resource_id, result, metadata)` row
- Append-only, retention 7 лет (compliance-ready)

### Prometheus
- `/metrics` endpoint через prometheus-net
- `node_*` (CPU, RAM, disk, net) от node-агентов
- `plexor_*` (внутренние счётчики: API latency, queue depth, scheduler decisions)

## Зависимости между модулями

```
Tenants       ← все читают tenant_id
Identity      ← все читают user/role/claims
Compute       → Network  (VPC/subnet lookup)
Compute       → Storage  (volume attach)
Compute       → Identity (ssh_keys lookup)
Network       → Identity (sg owner check)
Storage       → Identity (volume owner check)
Billing       ← все (subscribed to metering.used)
Telemetry     ← все (subscribed to audit + log events)
```

Правило: модуль-A → модуль-B **только через .Application**, никогда через
.Infrastructure (чтобы test-mocks не требовали реальную DB).

## Anti-patterns (запрещено в модуле)

- ❌ `module.Infrastructure → module.Domain` другого модуля (только через
  Contracts/Application).
- ❌ `Infrastructure` использует HTTP-клиент к другому модулю (используй
  Application слой через NATS или in-process).
- ❌ `Domain` знает про EF Core, ASP.NET, JSON.
- ❌ Прямой SQL — только через `IRepository<T>` (Dapper или EF Core).
- ❌ Глобальные statics для состояния (только DI).

## Plexor.Modules.Marketplace

**App provider catalog + instance lifecycle.** Это сердце marketplace
модели Plexor: позволяет пользователям устанавливать "приложения"
(WordPress, PostgreSQL, custom webapp) через декларативные templates.

App provider — **НЕ .NET plugin**, это YAML-файл с shell-командами.
Plexor интерпретирует manifest и применяет install/upgrade/uninstall
на нужных compute-нодах.

Подробнее о provider format: [providers.md](providers.md#2-app-providers-marketplace).

### Domain

```csharp
public sealed record AppProvider
{
    public required string Name { get; init; }        // 'wordpress'
    public required string Version { get; init; }     // '0.2.0'
    public required string DisplayName { get; init; } // 'WordPress'
    public required AppProviderSpec Spec { get; init; }
}

public sealed record AppProviderSpec
{
    public required ResourceRequirements Resources { get; init; }
    public required IReadOnlyList<ConfigParam> Config { get; init; }
    public LifecycleHooks Hooks { get; init; }
    public HealthCheckSpec? HealthCheck { get; init; }
}

public sealed record LifecycleHooks
{
    public required IReadOnlyList<ShellStep> Install { get; init; }
    public IReadOnlyList<ShellStep>? Upgrade { get; init; }
    public IReadOnlyList<ShellStep>? Uninstall { get; init; }
}

public sealed record ProviderInstance
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string ProviderName { get; init; }
    public required string ProviderVersion { get; init; }
    public required string InstanceName { get; init; }    // 'wp-7f3a2c'
    public required ProviderStatus Status { get; init; }  // installing | running | upgrading | failed | uninstalling
    public required JsonDocument Config { get; init; }    // user-supplied config
    public required JsonDocument Resources { get; init; } // allocated node, IP, ports
}

public enum ProviderStatus
{
    Installing,
    Running,
    Upgrading,
    Failed,
    Uninstalling,
}
```

### Application endpoints

```
GET    /api/v1/marketplace/providers
       → List installed app providers (from catalog)
       Response: [{ name, version, displayName, description, category, tier, icon }]

GET    /api/v1/marketplace/providers/{name}
       → Get provider details (config schema, resources, healthCheck)
       Response: { name, version, spec: {...}, dependencies: [...] }

POST   /api/v1/marketplace/providers
       Body: { source: 'github.com/me/my-provider', version: '0.1.0' }
       → Install a provider (git clone / OCI pull / tarball extract)

DELETE /api/v1/marketplace/providers/{name}
       → Uninstall provider (only if no running instances)

POST   /api/v1/marketplace/instances
       Body: { provider: 'wordpress', version: '0.2.0', config: { siteTitle: 'My Blog' } }
       → Deploy app instance
       Response: 202 Accepted with instance id

GET    /api/v1/marketplace/instances
       → List instances (scoped by tenant from JWT)
       Response: [{ id, provider, version, instanceName, status, url }]

GET    /api/v1/marketplace/instances/{id}
       → Get instance details + live status

DELETE /api/v1/marketplace/instances/{id}
       → Uninstall instance

POST   /api/v1/marketplace/instances/{id}/upgrade
       Body: { toVersion: '0.3.0' }
       → Upgrade instance to new provider version

GET    /api/v1/marketplace/instances/{id}/logs
       ?tail=N
       → Last N lines of provider install/upgrade logs (for debugging)
```

### Install workflow (state machine)

```
POST /instances
  ↓
validation (config against provider schema)
  ↓
resolve dependencies (auto-install missing providers)
  ↓
allocate resources (CPU, RAM, disk, IP, port via Compute/Network/Storage)
  ↓
write provider_instances row (status=Installing)
  ↓
publish 'plexor.app.install' to NATS
  ↓
[async] NodeAgent receives event:
   - reads provider.yaml from local cache (or pulls from source)
   - runs install hooks sequentially
   - publishes 'plexor.app.installed' with status
  ↓
control plane receives status event:
   - updates provider_instances.status
   - stores instance metadata (URLs, credentials)
  ↓
GET /instances/{id} returns 'running' with access URL
```

### Permission model

- **Provider install**: admin-only (operator role)
- **Instance install**: any tenant member, but instance is scoped to caller's tenant
- **Instance delete**: tenant admin or instance creator
- **Catalog browse**: any authenticated user

### Audit + metering

- Every `plx provider install-instance` writes `audit_log` row
  (who, what provider, what config)
- Running instances emit `metering` events for billing
  (CPU hours, RAM GB-hours, disk GB-hours, network egress)

### Example

```bash
# 1. Browse available providers
$ curl -H "Authorization: Bearer $JWT" http://plexor.local/api/v1/marketplace/providers | jq
[
  { "name": "wordpress", "version": "0.2.0", "displayName": "WordPress", "category": "cms", "tier": "official" },
  { "name": "postgresql", "version": "15.3.0", "displayName": "PostgreSQL", "category": "database", "tier": "official" },
  ...
]

# 2. Install WordPress
$ curl -X POST http://plexor.local/api/v1/marketplace/instances     -H "Authorization: Bearer $JWT"     -H "Content-Type: application/json"     -d '{"provider":"wordpress","version":"0.2.0","config":{"siteTitle":"My Blog","adminEmail":"jane@acme.com"}}'
{"id":"wp-7f3a2c","status":"installing"}

# 3. Wait for running
$ curl http://plexor.local/api/v1/marketplace/instances/wp-7f3a2c -H "Authorization: Bearer $JWT"
{"id":"wp-7f3a2c","status":"running","url":"http://203.0.113.42"}

# 4. Upgrade
$ curl -X POST http://plexor.local/api/v1/marketplace/instances/wp-7f3a2c/upgrade     -d '{"toVersion":"0.3.0"}' -H "Authorization: Bearer $JWT"
{"status":"upgrading"}

# 5. Delete
$ curl -X DELETE http://plexor.local/api/v1/marketplace/instances/wp-7f3a2c -H "Authorization: Bearer $JWT"
{"status":"uninstalling"}
```
