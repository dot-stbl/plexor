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
  install:
    compute: kvm              # kvm | lxd | pod | firecracker
    network: ovs              # ovs | cilium | host
    storage-block: ceph-rbd   # ceph-rbd | local-lvm
    storage-object: ceph-rgw  # ceph-rgw | minio
    state-db: postgresql      # postgresql (only option MVP)
    event-bus: nats           # nats (only option MVP)
  bootstrap:
    admin_email: admin@acme.internal
    admin_password: ${ADMIN_PASSWORD}
```

```bash
curl -fsSL https://get.plexor.dev | bash -s -- init -f -c plexor.yaml
```

## Что делает installer

Plexor installer выбирает **install providers** (infrastructure backends)
через system probes + user override. **App providers** (WordPress, etc.)
НЕ выбираются на install — они ставятся позже через marketplace.

1. **Discovery** — `SystemProbe` проверяет что есть в системе:
   - `/dev/kvm` exists? `libvirtd` running? VT-x support?
   - `openvswitch-switch` running? `cilium` kernel module loaded?
   - `ceph-mon` running? OSDs available? `lvm2` with thinpool?
   - `postgresql` reachable? `nats-server` running?

2. **Resolver** — для каждого install slot (compute / network / storage /
   state / bus) выбирает built-in provider. Если запрошенного provider
   нет в SystemProbe — подставляет с предупреждением (interactive)
   или auto-fallback (non-interactive).

3. **Planner** — генерирует план шагов с estimated duration:
   - enable-kvm-modules
   - bootstrap-ceph (or local-lvm fallback)
   - configure-ovs (or cilium fallback)
   - deploy-control-plane (Plexor.Host)
   - deploy-node-agent (Plexor.NodeAgent)
   - deploy-portal (web UI bundle)
   - configure-keycloak (or local auth)
   - create-default-tenant + bootstrap-admin
   - issue-tls (Let's Encrypt or self-signed)

4. **Apply** — выполняет шаги идемпотентно (`/var/lib/plexor/state.json`).
   Можно прервать и продолжить через `plx init` повторно.
5. **Handoff** — выводит URL, логин, initial credentials, recovery key,
   + предлагает установить первый app provider через marketplace.

## Single-node vs multi-node

### Single-node (ноутбук / dev / edge)

```
Single node provides compute+storage+control-plane
        ↓
plx init detects:
  - compute: pod → use K8s pod as VM (no real isolation, dev only)
  - storage-block: local-lvm → single-node, no replication
  - storage-object: minio → single-node S3
  - network: host → no overlay
  - state-db: postgresql (assumed present via apt)
  - event-bus: nats (assumed present via apt)
```

### 3-node production

```
3 servers (1 control + 2 compute):
  control:  Plexor.Host + NATS + Postgres + Keycloak
  compute1: Plexor.NodeAgent + KVM + OVS + Ceph-rbd client
  compute2: Plexor.NodeAgent + KVM + OVS + Ceph-rbd client
  + shared: Ceph cluster (3 monitors + OSDs on each)
        ↓
plx init detects:
  - compute: kvm → production default
  - storage-block: ceph-rbd → 3-node replicated
  - storage-object: ceph-rgw → 3-node replicated S3
  - network: ovs → production overlay
  - state-db: postgresql → dedicated node
  - event-bus: nats → JetStream enabled
```

### App providers (после Plexor install)

После того как Plexor установлен, юзер идёт в UI Marketplace и
устанавливает app providers. Эти **НЕ** часть installer — это
отдельный workflow (см. [providers.md](../providers.md#2-app-providers-marketplace)).

```bash
# UI: Marketplace → browse providers
# UI: pick WordPress → fill config → install-instance

# CLI equivalent:
plx provider list                    # see available providers
plx provider show wordpress           # see config schema
plx provider install-instance wordpress     --config siteTitle="My Blog"     --config adminEmail="jane@acme.com"
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