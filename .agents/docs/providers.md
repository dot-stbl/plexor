# Providers — два типа для двух задач

В Plexor есть **два разных типа** провайдеров, которые часто путают:

| Тип | Когда | Что | Дистрибуция |
|---|---|---|---|
| **Install providers** | При **установке** Plexor (ISO/CLI) | Как Plexor настроить своё хранилище/сеть | **Встроенный код** Plexor (НЕ плагин) |
| **App providers** | При **развертывании приложений** на Plexor | Как задеплоить user app (WordPress, Postgres, custom) | **Marketplace / template** (внешний автор) |

```
┌─────────────────────────────────────────────────────────────────┐
│  ISO / plx init / dashboard install                            │
│  ↓                                                               │
│  Plexor probes: "у меня есть Ceph или MinIO? OVS или Cilium?" │
│  ↓ user picks (or default)                                       │
│  Install provider SELECTED (e.g. ceph + ovs + kvm)              │
│  ↓ Plexor.Host configured for those backends                     │
└─────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────┐
│  Plexor running                                                  │
│  - Compute: KVM (built-in)                                       │
│  - Network: OVS (selected at install)                            │
│  - Storage: Ceph (selected at install)                          │
│  - State DB: PostgreSQL                                          │
│  - Marketplace: catalog of App providers                        │
│       ↓ user opens marketplace                                   │
│       ↓ installs App provider (e.g. wordpress provider)          │
│       ↓ Plexor deploys WordPress via Ceph volume + OVS network  │
│         using App provider's install steps                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. Install providers (built-in)

Install providers = **как Plexor настраивает своё окружение**. Они
**НЕ** плагины и **НЕ** NuGet-пакеты. Это код в Plexor, который
выбирается при установке через пробы и scoring.

### Доступные install providers (MVP)

| Категория | Provider | Альтернативы | Когда выбирается |
|---|---|---|---|
| **Compute** | `kvm` (default) | lxd, pod, firecracker | Linux + есть /dev/kvm |
| **Network** | `ovs` (default) | cilium, host (bridge-only) | Multi-VM нужен overlay |
| **Storage (block)** | `ceph-rbd` (default) | local-lvm | Multi-node; replication нужна |
| **Storage (object)** | `ceph-rgw` (default) | minio | S3-compatible нужен; multi-node |
| **State DB** | `postgresql` (default) | none в MVP | Всегда (single source of truth) |
| **Event bus** | `nats` (default) | none в MVP | Всегда |

### Выбор при установке

`plx init` или ISO installer запускает **probes**:

```bash
# Detect compute backend
$ plx init --probe
compute:  kvm (✓ /dev/kvm exists, libvirtd running, VT-x supported)
           lxd (✓ snap available)
           pod (✓ containerd available)
network:  ovs (✓ openvswitch-switch running)
           cilium (✗ no eBPF support in kernel)
storage:  ceph-rbd (✓ ceph-mon running, OSDs available: 3)
           local-lvm (✓ lvm2 + thinpool available)
...
```

User может либо принять defaults, либо override через `plexor.yaml`:

```yaml
# plexor.yaml — на каждом узле или на control plane
install:
  compute: kvm              # override default
  network: cilium           # explicitly choose cilium over ovs
  storage-block: local-lvm   # single-node setup, no need for ceph
  storage-object: minio     # single-node, no ceph-rgw
```

### Architecture: где живёт install provider code

```
Plexor.Host (control plane)
  └─ Plexor.Core.Providers/         ← built-in install provider SDK
       ├─ Compute/KvmComputeProvider.cs
       ├─ Compute/LxdComputeProvider.cs
       ├─ Compute/PodComputeProvider.cs
       ├─ Network/OvsNetworkProvider.cs
       ├─ Network/CiliumNetworkProvider.cs
       ├─ Storage/CephRbdStorageProvider.cs
       ├─ Storage/LocalLvmStorageProvider.cs
       ├─ Object/CephRgwObjectProvider.cs
       └─ Object/MinioObjectProvider.cs
```

Все эти провайдеры **вкомпилированы** в Plexor.Host. Никакой runtime
plugin loading. `plx init` выбирает между ними по feature probes
+ user override.

### Когда добавлять новый install provider

Только если нужен **новый класс инфры**, не покрытый текущими:
- `nomad` (orchestrator) — Phase 2
- `ironic` (bare metal) — Phase 3
- `vmware` (ESXi) — Phase 2 enterprise
- и т.д.

Добавление = новый .cs проект в `src/providers/Plexor.Providers.<Category>.<Name>/`,
реализующий соответствующий интерфейс. НЕ NuGet, НЕ plugin — просто
новый проект в солюшене.

---

## 2. App providers (marketplace)

App provider = **шаблон развертывания приложения** на Plexor'е.
Аналог: Helm chart, Replicated app, Coolify one-click.

Автор app provider'а описывает:
- Что развертывать (container image, helm chart, custom deploy)
- Какие ресурсы нужны (CPU, RAM, disk, ports)
- Какие параметры конфигурации
- Lifecycle: install / upgrade / uninstall
- Health check

**Распространение** (НЕ NuGet):
- Git repo (git clone / pull)
- OCI artifact (`plx provider install oci://ghcr.io/stbl/wordpress:0.2.0`)
- Tarball (`plx provider install ./wordpress-0.2.0.tar.gz`)
- Direct folder (`plx provider install ./wordpress-provider/`)
- HTTP artifact server (internal)

**НЕ NuGet.org** — Plexor providers не .NET-specific, это декларации
приложений.

### App provider format (YAML)

```yaml
# wordpress/provider.yaml
apiVersion: plexor.dev/v1
kind: AppProvider
metadata:
  name: wordpress
  version: 0.2.0
  displayName: WordPress
  description: Popular open-source content management system
  category: cms
  icon: https://.../wordpress.svg
  homepage: https://wordpress.org
  maintainer: jane.doe@example.com

spec:
  # Resource requirements (Plexor schedules based on these)
  resources:
    cpu: "500m"           # 0.5 cores
    memory: "512Mi"
    disk: "10Gi"
    ports:
      - port: 80
        protocol: TCP
        expose: true       # creates floating IP / ingress

  # Configuration parameters (user fills when installing)
  config:
    - name: siteTitle
      type: string
      required: true
      description: "WordPress site title"
    - name: adminEmail
      type: string
      required: true
      validation: email
    - name: databaseSize
      type: enum
      values: [small, medium, large]
      default: small
    - name: replicas
      type: integer
      default: 1
      min: 1
      max: 5

  # Dependencies on other providers / infrastructure
  dependencies:
    services:
      - name: postgresql
        provider: postgresql  # or 'mariadb' etc.
        version: ">=14.0"
        create: true           # Plexor auto-installs if not present
    - name: object-storage
      type: bucket
      size: 5Gi

  # Lifecycle hooks — shell commands run on the target node
  install:
    - name: pull-image
      run: |
        podman pull docker.io/library/wordpress:$version
    - name: create-config
      run: |
        cat > /var/lib/plexor/instances/$instanceId/wp-config.php <<EOF
        <?php
        define('DB_NAME', '$dbName');
        define('DB_USER', '$dbUser');
        define('DB_PASSWORD', '$dbPassword');
        ...
        EOF
    - name: start-container
      run: |
        podman run -d --name=$instanceId \
          -v /var/lib/plexor/instances/$instanceId:/var/www/html \
          -p $port:80 \
          -e WORDPRESS_DB_HOST=$dbHost \
          docker.io/library/wordpress:$version
    - name: wait-ready
      run: |
        until curl -sf http://localhost:$port/ > /dev/null; do
          sleep 2
        done

  upgrade:
    - name: backup
      run: |
        podman exec $instanceId wp db export > /var/lib/plexor/backups/$instanceId-$(date +%s).sql
    - name: rolling-update
      run: podman kill $instanceId && podman run ... (same as install.start-container)

  uninstall:
    - name: stop-container
      run: podman stop $instanceId && podman rm $instanceId
    - name: remove-volumes
      run: rm -rf /var/lib/plexor/instances/$instanceId
    - name: remove-floating-ip
      run: plx network floating-ip release $ipId

  # Health check — Plexor monitors this
  healthCheck:
    type: http
    endpoint: /
    port: 80
    expectedStatus: 200
    intervalSeconds: 30

  # UI metadata (icon, color, tier for marketplace display)
  ui:
    icon: wordpress
    color: "#21759b"
    tier: official            # official | community | verified
```

### Variables in commands

Plexor substitutes before running:
- `$instanceId` — auto-generated UUID
- `$version` — provider version
- `$port` — auto-allocated if config doesn't specify
- User-defined config values (e.g. `$siteTitle`)

### Discovery + install workflow

```bash
# 1. Browse marketplace
$ plx provider list
NAME              VERSION  CATEGORY  TIER
wordpress         0.2.0    cms       official
postgresql        15.3.0   database  official
redis             7.2.0    cache     community
nginx             1.25.2   web       official
minio             2024.04  object    official
ghost             5.85.1   cms       community
keycloak          24.0.5   identity  official
node-app          1.0.0    runtime   community

# 2. Show provider details
$ plx provider show wordpress
wordpress 0.2.0
  Popular open-source CMS
  Resources: 500m CPU, 512Mi RAM, 10Gi disk
  Ports: 80 (TCP, external)
  Config: siteTitle*, adminEmail*, databaseSize (small), replicas (1)
  Dependencies: postgresql >=14.0

# 3. Install
$ plx provider install wordpress
  ? Site title: My Blog
  ? Admin email: jane@example.com
  ? Database size: small
  ? Replicas: 1
  → Allocating compute on plexor-node-01
  → Allocating storage volume (10Gi)
  → Allocating floating IP
  → Pulling image wordpress:0.2.0
  → Starting container...
  → Health check passing
  ✓ WordPress instance 'wp-7f3a2c' running at http://203.0.113.42

# 4. List instances
$ plx provider instances
NAME          TYPE        VERSION  STATUS   URL
wp-7f3a2c      wordpress   0.2.0    running  http://203.0.113.42
pg-primary     postgresql  15.3.0   running  (internal)

# 5. Upgrade
$ plx provider upgrade wp-7f3a2c --to-version 0.3.0
  → Backing up database
  → Pulling image wordpress:0.3.0
  → Restarting container
  ✓ wp-7f3a2c now running 0.3.0

# 6. Uninstall
$ plx provider uninstall wp-7f3a2c
  → Stopping container
  → Removing volumes
  → Releasing floating IP
  ✓ wp-7f3a2c removed
```

### State persistence

Plexor tracks provider instances in `provider_instances` table:

```sql
CREATE TABLE provider_instances (
    id              UUID PRIMARY KEY,
    tenant_id       UUID NOT NULL,
    project_id      UUID,
    provider_name   TEXT NOT NULL,        -- 'wordpress'
    provider_version TEXT NOT NULL,       -- '0.2.0'
    instance_name   TEXT NOT NULL,        -- 'wp-7f3a2c'
    status          TEXT NOT NULL,        -- installing | running | upgrading | failed | uninstalling
    config          JSONB NOT NULL,       -- { "siteTitle": "My Blog", ... }
    resources       JSONB NOT NULL,       -- { "nodeId": "...", "ipAddress": "...", "ports": [...] }
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_provider_instances_tenant ON provider_instances(tenant_id, status);
```

### Catalog of community providers

A reference catalog at `https://catalog.plexor.dev` (or self-hosted):

```yaml
# catalog.yaml (in our org's repo or community-maintained)
providers:
  - name: wordpress
    source: https://github.com/stbl/plexor-provider-wordpress
    versions: [0.1.0, 0.2.0, 0.3.0]
    tier: official
  - name: postgresql
    source: https://github.com/stbl/plexor-provider-postgresql
    versions: [15.0, 15.1, 15.2, 15.3.0]
    tier: official
  - name: keycloak
    source: https://github.com/stbl/plexor-provider-keycloak
    versions: [24.0.5]
    tier: official
  - name: ghost
    source: https://github.com/community/plexor-provider-ghost
    versions: [5.85.1]
    tier: community
```

`plx provider list` reads this catalog and shows what's available.
Install pulls the source (git/OCI/tarball) and runs `provider.yaml`.

### Что НЕ в app provider scope

- ❌ Infrastructure drivers (KVM, Ceph, OVS) — это install providers, code в Plexor
- ❌ NuGet packages — Plexor providers не .NET-specific
- ❌ Vendor lock-in (cloud-specific runtimes) — providers должны быть portable
- ❌ Stateful multi-instance orchestration (kubernetes operators) — keep it simple shell-based

### Как автору создать app provider

```bash
# 1. Scaffold (template repo)
git clone https://github.com/stbl/plexor-provider-template
mv plexor-provider-template my-provider
cd my-provider

# 2. Edit provider.yaml
vim provider.yaml

# 3. Test locally
plx provider install .   # install from local folder
plx provider show my-provider
plx provider install-instance my-provider
plx provider uninstall-instance my-instance

# 4. Publish
git push origin main
# (or)
podman build . -t ghcr.io/me/my-provider:1.0.0
podman push ghcr.io/me/my-provider:1.0.0
# (or)
tar czf my-provider-1.0.0.tar.gz .
```

---

## См. также

- [architecture.md](architecture.md) — где живут provider коды и instances
- [modules.md](modules.md) — Marketplace module (instance management)
- [scope.md](scope.md) — какие install + app providers в MVP
- [operations/install.md](operations/install.md) — `plx init` flow
