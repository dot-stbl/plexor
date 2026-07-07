# Install

Plexor ставится одной командой `plx init` через пять стадий: Discovery →
Resolver → Planner → Apply → Handoff.

## Quick start

```bash
# Скачать CLI
curl -fsSL https://get.plexor.dev | bash
# либо
brew install plexor-dev/tap/plx

# Установить кластер
plx init
```

`plx init` запускает интерактивный мастер:
1. Cluster name + endpoint
2. Auto-detected providers (с возможностью переопределить)
3. Bootstrap admin
4. Подтверждение плана
5. ~10-15 минут установки

## Non-interactive install

```yaml
# plexor.yaml
apiVersion: plexor.dev/v1
kind: Cluster
metadata:
  name: company-prod
  endpoint: cloud.acme.internal
spec:
  providers:
    compute: kvm
    storage: ceph
    network: ovs
    os: talos
    orchestrator: k3s
    auth: keycloak
  bootstrap:
    admin_email: admin@acme.internal
    admin_password: ${ADMIN_PASSWORD}
```

```bash
curl -fsSL https://get.plexor.dev | bash -s -- init -f -c plexor.yaml
```

## Что делает installer

1. **Discovery** — `SystemProbe` проверяет что есть: KVM, OVS, Ceph,
   Docker, IP addresses, public DNS, etc.
2. **Resolver** — для каждого слоя (compute/storage/network/os) выбирает
   провайдера. Если запрошенного провайдера нет — подставляет с
   предупреждением (interactive) или auto-fallback (non-interactive).
3. **Planner** — генерирует план шагов с estimated duration:
   - install-k3s
   - bootstrap-ceph
   - configure-ovs
   - install-libvirt
   - deploy-control-plane
   - deploy-portal
   - configure-keycloak
   - create-default-tenant
   - issue-tls
4. **Apply** — выполняет шаги идемпотентно (`/var/lib/plexor/state.json`).
   Можно прервать и продолжить через `plx init` повторно.
5. **Handoff** — выводит URL, логин, initial credentials, recovery key.

## Single-node vs multi-node

### Single-node (ноутбук / dev / edge)

```
Single node provides compute+storage+control-plane
        ↓
plx init detects:
  - compute: pod → use K8s pod as VM (no real isolation, dev only)
  - storage: minio → single-node S3
  - network: host → no overlay
  - os: pod-template → no host OS needed
```

### 3-node production

```
3 servers (1 control + 2 compute):
  control:  Plexor.Host + NATS + Postgres + Keycloak
  compute1: Plexor.NodeAgent + KVM + OVS
  compute2: Plexor.NodeAgent + KVM + OVS
        ↓
plx init detects:
  - compute: kvm → production default
  - storage: ceph → 3-node replicated
  - network: ovs → production overlay
  - os: talos → immutable, API-managed
```

## Air-gapped install

```bash
# Pre-download artifacts
plx artifact bundle --offline --output ./plexor-bundle.tar.gz

# Copy to air-gapped host, install
plx init --bundle ./plexor-bundle.tar.gz --config plexor.yaml
```

## Recovery

Если installer упал в середине:

```bash
plx init   # автоматически продолжит (state.json)

# Полный reset (everything)
plx destroy --keep-data=false

# Reset state без удаления данных
plx state reset
```

## Troubleshooting

См. [troubleshooting.md](troubleshooting.md).