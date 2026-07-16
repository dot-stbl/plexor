# Runtime, capabilities & networking — overview

> **Read this when** ты запутался в общей картине "что такое Plexor и
> как ноды/CP/workloads/сеть соотносятся". Собирает ответы в одно место
> из существующих документов, не заменяет их.
>
> **Status:** Phase B+ закончен. Phase D+ не начат. Overview собирает
> что уже решено vs что open question.
>
> **Читать вместе с:** [../architecture.md](../architecture.md) (общая
> картина), [networking.md](../networking.md) (mesh / overlay / DNS),
> [runtimes-and-bindings.md](../runtimes-and-bindings.md) (Runtime/Binding
> философия), [../../plans/plan-runtime-providers.md](../../plans/plan-runtime-providers.md)
> и [../../plans/plan-k8s.md](../../plans/plan-k8s.md) (implementation plans).

---

## TL;DR — за 30 секунд

Plexor = **control plane (`Plexor.Host`) + N compute-нод (`Plexor.NodeAgent`)**, соединённых **WireGuard mesh**. Нода сообщает **capabilities** (что умеет запускать: KVM? LXC? Docker? k3s?). Control plane **schedules** workloads на ноды по `service.validRuntimes ∩ node.availableRuntimes ∩ userPreference`. Внутри ноды workloads получают **overlay IP** (VXLAN) для cross-node L2. **Public IP** — **отдельная** сущность, не auto-allocated, привязывается к workload явно.

**Нюанс:** workloads на CP — да, можно. CP = нода с собственным NodeAgent. Один бинарь, развернут дважды.

---

## 1. Уровни виртуализации и capability detection

### Что у нас уже решено

Plexor **уже описывает** разные классы runtime-ов в двух местах:

- [providers.md](../providers.md) §1 — **Install providers**:
  - Compute: `kvm` (default), `lxd`, `pod`
  - Network: `ovs` (default), `cilium`, `host` (bridge-only)
  - Storage: `ceph-rbd` / `local-lvm`; Object: `ceph-rgw` / `minio`
  - State: `postgresql` (always); Event: `nats` (always)
- [runtimes-and-bindings.md](../runtimes-and-bindings.md) §2 — **Runtime** classes для **workload**-ов (не для нод):
  - **Direct**: `vm` (KVM), `lxc`, `docker` — Plexor владеет lifecycle
  - **Delegated**: `k8s` — Plexor — tenant внутри чужого оркестратора

### Что мы НЕ описали явно — **open question**

**Нода сообщает capabilities в join** — не описано явно. Текущее:

- `NodeJoinCommand` принимает `Hostname, Role, NodeSpec (cpu, ram, disk, providers[])`
- **Что такое `providers[]`?** В `NodeSpec.Providers` — `string[]`. Глянем:
  ```csharp
  public sealed record NodeSpec(
      int Cores,
      long RamBytes,
      long DiskBytes,
      string[] Providers);
  ```
  Это **просто массив строк** — `"kvm"`, `"docker"`, `"lxc"`. Нет **автоматического detection**, нет структурированного набора capability-флагов. **Нода НЕ обнаруживает себя сама** — operator декларирует capabilities через `plx node install` / config.

**Что мы не описали:**
1. **Auto-detect на ноде**: `kvm-ok` (есть `/dev/kvm`), `nested-virt` (если нода в VM), `docker`, `lxc`, `k3s-installed`. Сейчас operator говорит вручную — drift-prone.
2. **Constraints по nested virt**: KVM-нода внутри KVM-VM НЕ может запустить nested KVM. Docker-нода может запустить только контейнеры. Это **не описано явно** в нашей модели — placement просто берёт `intersect(validRuntimes, nodeProviders)`.
3. **Re-registration при capability change**: нода обновляет kernel module → capability меняется → как CP узнаёт? Сейчас только на heartbeat.

### Решение: как это должно работать (набросок)

В [providers.md §probes](../providers.md#выбор-при-установке) уже есть **install-time probes** (`plx init` запускает probes на нашей машине). Тот же паттерн применим к ноде:

```bash
# На ноде при join (или периодически)
$ plx node probe
compute:
  kvm:        ✓ /dev/kvm exists, libvirtd running
  lxc:        ✓ lxc-ls available
  docker:     ✓ dockerd running
  k3s:        ✗ not installed
network:
  ovs:        ✓ openvswitch-switch running
  cilium:     ✗ no eBPF support in kernel
storage:
  ceph-rbd:   ✗ no ceph-mon
  local-lvm:  ✓ thinpool available
```

**Что нужно:** расширить `NodeSpec` до `NodeCapabilities` record со structured capabilities. `NodeAgent` запускает probe раз в N часов (или при изменении `/proc/cpuinfo`, kernel modules). CP обновляет `node.capabilities`. Scheduler использует `capabilities` вместо `string[] Providers`.

**Open**: кто делает эту работу — `plan-runtime-providers` Phase 2+, или отдельный `plan-capabilities`?

---

## 2. Control plane как нода

### Что у нас уже решено

Plexor.Host = **control plane + собственный NodeAgent в одной дистрибуции**. Это нигде не написано явно, но **следует из кода**:

- `plan-clusters` коммиты содержат `Plexor.NodeAgent` worker, который ставится на каждой ноде **включая** хост. Нет специальной "host mode" — просто "host has its own NodeAgent".
- `node.yaml` на каждой ноде (включая host) декларирует control plane URL. На host это `https://localhost:48001` (self).
- В нашем текущем `Program.cs` host — это просто `Plexor.Host` + `AddHostedService<PlexorCaStartup>` + Kestrel на 48001/48002. **Отдельного флага "this is the host" нет** — просто host имеет минимальную конфигурацию.

**Подтверждено в планах:**
- `plan-clusters` уже пишет `NodeAgentWorker` как generic (для любой ноды).
- `plan-runtime-providers` §"Cluster integration" добавляет `RuntimeId` в Cluster — **тоже не делит на "host has runtime" vs "node has runtime"**, просто кластер = нода, у которой **может быть** runtime.

### Что мы НЕ описали явно — **open question**

1. **Может ли CP запускать workloads?** Из кода — **может** (это просто нода с capabilities). Из доков — **не описано**. `runtimes-and-bindings.md` §"Cluster integration" — cluster is just a node with a runtime. CP — это cluster-zero. Значит **может**. Но явно в `architecture.md`:
   > "DB-of-record = PostgreSQL. Ядро пишет state в Postgres, **node-агенты только исполняют команды** и репортят обратно."

   Это **противоречит** — node-агенты исполняют команды, значит CP-as-node-agent тоже. Либо "node-agent" в этом контексте = "включая CP". Решаемо: уточнить в `architecture.md`.

2. **Какие default capabilities у host?** Условно: KVM (если есть) + LXC + Docker (Podman в нашей реализации) — **на хосте по умолчанию может запускать всё direct-класс**. Но явно не прописано.

3. **Разделение ответственности**: CP отвечает за API/DB/auth. Если CP сам запускает workload — workload manager живёт на той же машине. **Не запрещено**, но усложняет мониторинг (CP упал = workload management упал). Это компромисс **single-binary**.

### Решение: как должно работать

**Прямо сейчас:** CP **может** запускать workloads, потому что это просто ещё одна нода в mesh. Нет специальной логики. Документация [architecture.md](../architecture.md) §Control plane должна быть обновлена чтобы отразить это явно — "control plane IS a node, has its own NodeAgent + capabilities, can host workloads".

**Open**: должен ли CP-only mode (без runtime capabilities) быть default? Если да — auto-detect при install может выставить "no compute" capabilities на CP-only deployment, и workloads автоматически пойдут на ноды. Если CP сам с KVM — workloads могут идти на него. Политика.

---

## 3. Сетевая модель (mesh + overlay + public + DNS + VPN)

### Что у нас уже решено — **подробно в [networking.md](../networking.md)**

У нас есть **5 слоёв**:

| Слой | Технология | Subnet | Назначение |
|---|---|---|---|
| **Mesh** | WireGuard | `10.200.0.0/16` | control plane ↔ nodes (UDP 51820, NAT-traversing) |
| **Overlay** | VXLAN (OVS) | `10.100.0.0/16` | workloads ↔ workloads cross-node (L2) |
| **Internal DNS** | CoreDNS | `*.plexor.internal` | name resolution внутри mesh (10.200.0.0/16) |
| **Public endpoints** | (none yet) | — | workloads, exposed externally |
| **External DNS** | (delegated) | — | для public domain (`plexor.example.com`) |

Это **хорошо** описано в `networking.md` — четыре секции (WireGuard, VXLAN, DNS, zero-touch join) + failure modes + config.

### Что мы НЕ описали — **open question**

**Public IP** — **самая сырая часть**:
- В [networking.md §3](../../docs/architecture/networking.md) public IPs упомянуты вскользь (auto-registration "if exposed") — **нет модели**.
- В [runtimes-and-bindings.md §"Binding"](../../docs/architecture/runtimes-and-bindings.md) binding упоминает "open сетевой путь (SG/firewall)" — но не описано **как именно**.
- В [providers.md §"wordpress" example](../../docs/providers.md) `expose: true # creates floating IP / ingress` — **floating IP** упоминается, но не определён.

**Что отсутствует:**

1. **Модель Public IP / Floating IP**: это resource с lifecycle? IP allocation pool? Source — on-prem public block? Cloud provider integration (BYOIP)? Не описано.
2. **NAT / port forwarding**: workloads не имеют public IP автоматически. Кто-то должен открыть 80/tcp на edge firewall + DNAT. **Не описано**.
3. **Load Balancer**: "Managed Kubernetes" в плане обещает LB. В `plan-k8s.md` LB не детализирован. В `runtimes-and-bindings.md` binding-контракт для workload LB — намечен но не реализован.
4. **VPN для админ-доступа** (WireGuard до CP — это mesh для **нод**, не для **юзеров**). Юзер получает доступ через `https://plexor.example.com` (web UI), но если нужно **SSH-style admin к ноде** — не описано.
5. **Multi-tenant network isolation**: в [networking.md §1 Address plan](../../docs/architecture/networking.md) есть `10.0.0.0/8` "Reserved for future (external VPN, public IPs, etc.)" — но **что** резервируется не сказано. Tenant A может видеть Tenant B workloads в overlay? Сейчас — **да, может** (overlay общий). Это **security gap** не описанный.

### Решение: что нужно

- **Floating IP resource**: новая entity `FloatingIp` с lifetime (issued_at, expires_at?), pool, attachment к workload. Phase 5+ (вне Phase B+).
- **Edge gateway / NAT layer**: один узел-роль "edge" с публичными NIC + DNAT rules. Не описано.
- **Tenant network isolation**: VRF-подобное разделение через отдельные bridge-ы / VXLAN VNI per tenant. Сейчас **single broadcast domain** — security issue для multi-tenant Phase 2+.

---

## 4. Зоны / VPC / изоляция

### Что у нас уже решено — **намечено, не построено**

В [runtimes-and-bindings.md §6 "Binding"](../../docs/architecture/runtimes-and-bindings.md) написано:
> **Binding = модель безопасности.** Плоский mesh, где все достают всех —
> дыра. Поэтому **default-deny**, а binding — единственный способ
> открыть путь (микросегментация через связи).

Идея: workloads не могут общаться друг с другом **пока не объявлен binding** между ними. Это на уровне идеи.

### Что мы НЕ описали — **open question**

1. **Что такое "зона"?** В `architecture.md` есть `multi-zone` mention — не определено. Это:
   - VPC (virtual private cloud) — L2 isolation?
   - Region — geographic separation?
   - Security zone — набор workloads с shared policy?
   - Tenant — multi-tenant isolation?
   
   **Все четыре варианта перепутаны** в разных местах.

2. **VPC как сущность**: `network/vpc` URL из брифа `04-network-vpc.md` есть в UI roadmap, но **в docs не описано как это строится**. Это просто overlay подсеть? Или отдельный bridge? Или tenant-scoped VXLAN?

3. **Изоляция по умолчанию**: если binding = default-deny, что делает workloads в одном overlay? Они могут общаться через overlay IP — но binding = нет. **Противоречие** в нашей модели: binding говорит "не общаются" а overlay говорит "общаются".

4. **Security groups / firewall rules**: stateful фильтрация между workloads. **Не описано**.

### Решение: что нужно

- **Определить VPC как ресурс** (entity + state machine + REST surface + capabilities). Это Phase 5+ но нужно архитектурно решить **до** Phase D+ (workload controller), чтобы scheduler учитывал.
- **Tenant isolation default**: каждый tenant получает свой VXLAN VNI / overlay subnet. Это требует переделки `networking.md` чтобы пересечение mesh + VXLAN работало per-tenant.
- **Binding-as-firewall**: реализовать binding как iptables/nftables rules в overlay, а не просто DNS-имя. Сейчас binding только DNS.

---

## 5. Public endpoints (DNS + VPN + публичные адреса)

### Что у нас уже решено — **частично**

**Internal DNS** (CoreDNS) — **полностью** описан в [networking.md §3](../../docs/architecture/networking.md). Работает для `*.plexor.internal`. Auto-registration через NATS events при создании нод/VM/instance.

**External DNS** — намечен как Phase 2+ в `networking.md`:
> Plexor can integrate with external DNS (Route53, Cloudflare, PowerDNS)
> for public-facing app instances. Users configure their domain
> delegation to Plexor's nameservers. Plexor pushes records via API.
> 
> For MVP: external DNS is user-managed (manually add A records).

То есть: MVP — ручной A-record. Phase 2+ — auto-push в Route53/Cloudflare.

**User-facing VPN** — **не описано**. Только **node mesh** (WireGuard для **нод ↔ CP**). Для **юзер-доступа** к Plexor UI — HTTPS (web). Для **admin-доступа к нодам** — **не описано** (должен быть SSH по IP из mesh? Bastion?).

### Что мы НЕ описали — **open question**

1. **Публичные адреса workloads**: см. §3 — нет модели Floating IP. Workload с `expose: true` в provider.yaml — **как именно** это становится публично доступным? (см. §3)
2. **Edge gateway / ingress controller**: общая точка входа, которая DNAT-ит публичный IP → workload IP. В `plan-k8s.md` для k8s упоминается Traefik (bundled with k3s) — но это **только для k3s workload-ов**, не для всей Plexor.
3. **Multi-region / multi-DC**: `architecture.md` упоминает "multi-region" в extraction tier. Не описано как.
4. **Service mesh**: для east-west traffic между workloads. Не описано.

### Решение: что нужно

- **Public IP + Edge gateway resource** (Phase 5+). Описывает:
  - FloatingIp entity (lifecycle, attach to workload, release)
  - EdgeGateway entity (на какой ноде публичные NIC, capacity)
  - DNAT rules generation
- **External DNS auto-push** (Phase 2+) — вынести в отдельный план.

---

## 6. Что мы ещё НЕ знаем / нужно решить

Все open questions, собранные в одном месте:

| # | Open question | Где нужно решить | Phase |
|---|---|---|---|
| **C-1** | Capability auto-detect на ноде (vs manual `Providers[]`) | `plan-runtime-providers` v2 или new `plan-capabilities` | Phase 2+ |
| **C-2** | Constraints: KVM-в-VM ≠ KVM-на-baremetal. Как формализовать в capabilities | same as C-1 | Phase 2+ |
| **CP-1** | Может ли CP запускать workloads (явно описать в `architecture.md`) | обновить `architecture.md` + `STATE.md` | сейчас |
| **CP-2** | Какие default capabilities у host, как auto-detect | same as C-1 | Phase 2+ |
| **N-1** | **Public IP / Floating IP модель** | new `plan-public-endpoints` | Phase 5+ |
| **N-2** | **Edge gateway / NAT layer** | same as N-1 | Phase 5+ |
| **N-3** | **Tenant network isolation** в shared VXLAN | new `plan-network-isolation` | Phase 5+ (нужно до multi-tenant UI) |
| **N-4** | **VPC как ресурс** (entity + state machine + REST) | same as N-3 | Phase 5+ |
| **N-5** | **Service mesh / east-west firewall** | future | Phase 7+ |
| **N-6** | **Admin VPN** (доступ к нодам для SSH/operator) | new `plan-admin-access` | Phase 5+ |
| **B-1** | **Binding как firewall**, не только DNS | new `plan-binding-firewall` | Phase 5+ |

---

## 7. Что НЕ меняется — open architecture

Некоторые вещи **уже зафиксированы** (ADR-стиль, в доках не пересматриваем без серьёзного повода):

| Решение | Где зафиксировано | Почему locked |
|---|---|---|
| WireGuard для mesh | [networking.md §1](../../docs/architecture/networking.md) | NAT-traversing, kernel module, low CPU |
| VXLAN через OVS | [networking.md §2](../../docs/architecture/networking.md) | "L2 connectivity для cluster workloads (k8s, Galera)" |
| CoreDNS для internal | [networking.md §3](../../docs/architecture/networking.md) | "single binary, well-tested, auto-registration" |
| **Система из 2 install providers** (built-in + marketplace app) | [providers.md](../../docs/providers.md) §вступление | "не путать install providers с app providers" |
| **Direct vs Delegated runtime** (vm/lxc/docker vs k8s) | [runtimes-and-bindings.md §2](../../docs/architecture/runtimes-and-bindings.md) | "k8s = сам оркестратор, не peer" |
| **Service / Runtime / Binding разделение** | [runtimes-and-bindings.md §1](../../docs/architecture/runtimes-and-bindings.md) | "транслятор, не изобретатель" |
| **Capability-флаги гонят placement, не человек** | [runtimes-and-bindings.md §3](../../docs/architecture/runtimes-and-bindings.md) | "stateful → direct-класс автоматически" |
| **default-deny binding** | [runtimes-and-bindings.md §6](../../docs/architecture/runtimes-and-bindings.md) | "binding = модель безопасности" |
| **mTLS для control plane ↔ node** | `plan-mvp-secure-deploy` + текущий код | "Plexor is the only public-facing app" |
| **10y cert TTL, no rotation** | BACKEND-ISSUES.md | "MVP" |
| **Схемы per-module** (`sigil`, `realm`, `forge`, etc.) | [architecture.md §принципы](../../docs/architecture.md) | "одна база, разные схемы" |
| **10.200.0.0/16 + 10.100.0.0/16** address plan | [networking.md §1](../../docs/architecture/networking.md) | задокументирован в примерах wg-quick |
| **UDP 51820 для WireGuard** | [networking.md §1](../../docs/architecture/networking.md) | "99% NAT traversal" |

---

## 8. Где это в коде прямо сейчас

| Слой | Состояние | Где |
|---|---|---|
| Control plane entry (Host) | ✅ Phase B done | `src/host/Plexor.Host/` |
| mTLS между CP и node | ✅ Phase B done | `src/shared/security/Plexor.Shared.Mtls/` |
| Node identity + cert issuance | ✅ Phase B done | `clusters/join` flow + `Plexor.Shared.Identifiers/` |
| NodeAgent mTLS client | ✅ Phase B done | `src/agents/Plexor.NodeAgent/Composition/MtlsHttpHandlerFactory.cs` |
| Plexor config stack (PLX_ env + TOML + dev-certs) | ✅ done | `src/shared/infra/Plexor.Shared.Configuration/` |
| **Workload controller (Docker/K3s/Podman)** | ❌ Phase D, not started | `plan-runtime-providers` v0.1 |
| **WireGuard mesh** | ❌ not implemented (только design) | `networking.md` |
| **VXLAN overlay** | ❌ not implemented | `networking.md` |
| **CoreDNS integration** | ❌ not implemented | `networking.md` |
| **Capability auto-detect** | ❌ not implemented | this doc, §1 |
| **Floating IP / Edge gateway** | ❌ not implemented | this doc, §3 |
| **VPC / tenant isolation** | ❌ not implemented | this doc, §4 |
| **Binding firewall** | ❌ not implemented | this doc, §4 |
| **K3s integration (plan-k8s)** | ❌ not started | `plan-k8s.md` |

---

## 9. TL;DR для занятых — что дальше

**Phase D (Workloads)** в plan-mvp-secure-deploy — **первый** practical шаг после Phase B. Но прежде чем его начать, **нужно зафиксировать**:

1. **Capability auto-detect** (`plan-capabilities` или v2 `plan-runtime-providers`) — без этого scheduler не знает куда ставить workload
2. **Floating IP + edge gateway** (`plan-public-endpoints`) — без этого workloads не получают public endpoints (но это Phase 5+ не Phase D)

**Для Phase D MVP** достаточно:
- Нода уже может декларировать `Providers[]` (хоть и руками)
- Scheduler уже пересекает `validRuntimes ∩ nodeProviders`
- Workload lifecycle: render manifest → deploy via SSH → poll state
- Один runtime MVP: Docker Compose (single-host)

То есть **Phase D можно начинать уже сейчас**, принимая что capability detection + public IPs — follow-up.

---

## 10. См. также

- [../architecture.md](../architecture.md) — общая картина
- [networking.md](../networking.md) — mesh / overlay / DNS детально
- [runtimes-and-bindings.md](../runtimes-and-bindings.md) — Runtime/Binding философия
- [../../plans/plan-mvp-secure-deploy.md](../../plans/plan-mvp-secure-deploy.md) — Phase B done, D-F open
- [../../plans/plan-runtime-providers.md](../../plans/plan-runtime-providers.md) — implementation plan для workload-ов
- [../../plans/plan-k8s.md](../../plans/plan-k8s.md) — K3s integration
- [../../plans/plan-clusters/SUMMARY.md](../../plans/plan-clusters/SUMMARY.md) — что уже реализовано
- [`../../STATE.md`](../../STATE.md) — canonical position (drift-prone, надо обновить)
